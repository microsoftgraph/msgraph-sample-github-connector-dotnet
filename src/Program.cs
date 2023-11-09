// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.CommandLine;
using GitHubConnector;
using GitHubConnector.Extensions;
using GitHubConnector.Models;
using GitHubConnector.Services;
using Microsoft.Graph;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.Graph.Models.ODataErrors;
using Octokit;

// Pass this option to run in simplified admin mode.
// This will setup a webhook on the port specified in appsettings.json to
// receive signals from the Teams admin center.
var useSimplifiedAdminOption = new Option<bool>(new[] { "--use-simplified-admin", "-u", })
{
    Description = "Run the connector in simplified admin mode.",
    IsRequired = false,
};

var command = new RootCommand();
command.AddOption(useSimplifiedAdminOption);

command.SetHandler(async (context) =>
{
    var useSimplifiedAdmin = context.ParseResult.GetValueForOption(useSimplifiedAdminOption);

    try
    {
        var settings = AppSettings.LoadSettings();

        var connectorService = new SearchConnectorService(settings);
        var repoService = new RepositoryService(settings);

        if (useSimplifiedAdmin)
        {
            var teamsAppConfigService = new M365AppConfigService(settings);
            await ListenForSimplifiedAdminAsync(connectorService, repoService, teamsAppConfigService);
        }
        else
        {
            await RunInteractivelyAsync(connectorService, repoService);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
});

Environment.Exit(await command.InvokeAsync(args));

// Run the application in simplified admin mode.
static async Task ListenForSimplifiedAdminAsync(
    SearchConnectorService connectorService,
    RepositoryService repoService,
    M365AppConfigService m365AppConfigService)
{
    var listener = m365AppConfigService.Start();

    var keepListening = true;
    while (keepListening)
    {
        try
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            ConnectorResourceData? connectorData = null;
            if (request.HttpMethod == "POST")
            {
                Console.WriteLine("Received post");

                // Parse the POST body
                var changeNotifications = m365AppConfigService.DeserializePostBody(request.InputStream);

                // Ensure the body deserialized into the expected form
                // and that the validation tokens are valid.
                if (changeNotifications is not null &&
                    changeNotifications.Value is not null &&
                    await m365AppConfigService.ValidateTokensAsync(changeNotifications.ValidationTokens))
                {
                    // Parse the resourceData field
                    connectorData = ConnectorResourceData.CreateFromAdditionalData(
                        changeNotifications.Value.First().ResourceData?.AdditionalData);
                }
            }

            // Return 202 so Microsoft Graph won't retry notification
            response.StatusCode = 202;
            response.Close();

            if (connectorData is not null)
            {
                // Get any existing connections
                // Connections associated with the simplified admin experience should
                // have a connectorId property set to the Teams app ID
                var connectorId = connectorData.Id ?? throw new ArgumentException(nameof(connectorData.Id));
                Console.WriteLine($"Checking for existence of connection with connector ID: {connectorId}");
                var existingConnections = await connectorService.GetConnectionsAsync();

                ExternalConnection? existingConnection = null;
                if (existingConnections is not null && existingConnections.Value?.Count > 0)
                {
                    existingConnection = existingConnections.Value?.SingleOrDefault(c => c.ConnectorId == connectorId);
                }

                Console.WriteLine($"Connection exists? {(existingConnection is null ? "NO" : "YES")}");

                if (connectorData.State == "enabled")
                {
                    Console.WriteLine("Request is to create new connection");

                    // Only create if a connection doesn't already exist
                    if (existingConnection is null)
                    {
                        // Create the connection with the connectors ticket
                        // and connectorId
                        await connectorService.CreateConnectionAsync(
                            "GitHubIssuesM365",
                            "GitHub Issues for M365 App",
                            "This connector was created by an M365 app",
                            "issues",
                            connectorData.ConnectorsTicket,
                            connectorId);
                        Console.WriteLine("Created connection successfully");

                        // Register the schema
                        await connectorService.RegisterSchemaAsync(
                            "GitHubIssuesM365", SearchConnectorService.IssuesSchema);
                        Console.WriteLine("Registered schema");
                    }
                }
                else
                {
                    Console.WriteLine("Request is to delete connection");
                    if (existingConnection is not null)
                    {
                        // Delete the connection
                        await connectorService.DeleteConnectionAsync(existingConnection.Id);
                        Console.WriteLine("Deleted connection successfully");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            keepListening = false;
        }
    }

    // Stop the HTTP listener
    m365AppConfigService.Stop();
}

// Run the app interactively
static async Task RunInteractivelyAsync(
    SearchConnectorService connectorService, RepositoryService repoService)
{
    ExternalConnection? currentConnection = null;
    try
    {
        do
        {
            var choice = Interactive.DoMenuPrompt(currentConnection);

            switch (choice)
            {
                case MenuChoice.CreateConnection:
                    var newConnection = await CreateConnectionInteractivelyAsync(connectorService);
                    currentConnection = newConnection ?? currentConnection;
                    break;
                case MenuChoice.SelectConnection:
                    var selectedConnection = await SelectConnectionInteractivelyAsync(connectorService);
                    currentConnection = selectedConnection ?? currentConnection;
                    break;
                case MenuChoice.DeleteConnection:
                    if (currentConnection is not null)
                    {
                        await DeleteConnectionInteractivelyAsync(connectorService, currentConnection.Id);
                        currentConnection = null;
                    }
                    else
                    {
                        Console.WriteLine("No connection selected. Please create a new connection or select an existing connection.");
                    }

                    break;
                case MenuChoice.RegisterSchema:
                    if (currentConnection is not null)
                    {
                        await RegisterSchemaInteractivelyAsync(connectorService, currentConnection.Id);
                    }
                    else
                    {
                        Console.WriteLine("No connection selected. Please create a new connection or select an existing connection.");
                    }

                    break;
                case MenuChoice.PushAllItems:
                    if (currentConnection is not null)
                    {
                        await PushItemsInteractivelyAsync(connectorService, repoService, currentConnection.Id);
                    }
                    else
                    {
                        Console.WriteLine("No connection selected. Please create a new connection or select an existing connection.");
                    }

                    break;
                case MenuChoice.Exit:
                    return;
                default:
                    Console.WriteLine("Invalid choice!");
                    break;
            }
        }
        while (true);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}

static async Task<ExternalConnection?> CreateConnectionInteractivelyAsync(SearchConnectorService connectorService)
{
    var connectionId = Interactive.PromptForInput(
        "Enter a unique ID for the new connection (3-32 characters)", true) ?? string.Empty;
    var connectionName = Interactive.PromptForInput(
        "Enter a name for the new connection", true) ?? string.Empty;
    var connectionDescription = Interactive.PromptForInput(
        "Enter a description for the new connection", false);
    var itemType = Interactive.PromptForItemType();

    try
    {
        var connection = await connectorService.CreateConnectionAsync(
            connectionId, connectionName, connectionDescription, itemType);

        Console.WriteLine($"New connection created - Name: {connection?.Name}, Id: {connection?.Id}");
        return connection;
    }
    catch (ODataError oDataError)
    {
        Console.WriteLine($"Error creating connection: {oDataError.ResponseStatusCode}");
        Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
        return null;
    }
}

static async Task<ExternalConnection?> SelectConnectionInteractivelyAsync(SearchConnectorService connectorService)
{
    Console.WriteLine("Getting existing connections...");
    try
    {
        var response = await connectorService.GetConnectionsAsync();
        var connections = response?.Value ?? new List<ExternalConnection>();
        if (connections.Count <= 0)
        {
            Console.WriteLine("No connections exist. Please create a new connection.");
            return null;
        }

        var selectedIndex = Interactive.PromptToSelectConnection(connections);

        return connections[selectedIndex];
    }
    catch (ODataError oDataError)
    {
        Console.WriteLine($"Error getting connections: {oDataError.ResponseStatusCode}");
        Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
        return null;
    }
}

static async Task DeleteConnectionInteractivelyAsync(SearchConnectorService connectorService, string? connectionId)
{
    _ = connectionId ?? throw new ArgumentNullException(nameof(connectionId));

    try
    {
        await connectorService.DeleteConnectionAsync(connectionId);
        Console.WriteLine("Connection deleted successfully.");
    }
    catch (ODataError oDataError)
    {
        Console.WriteLine($"Error deleting connection: {oDataError.ResponseStatusCode}");
        Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
    }
}

static async Task RegisterSchemaInteractivelyAsync(SearchConnectorService connectorService, string? connectionId)
{
    _ = connectionId ?? throw new ArgumentNullException(nameof(connectionId));

    var itemType = Interactive.PromptForItemType();
    Console.WriteLine("Registering schema, this may take some time...");

    try
    {
        await connectorService.RegisterSchemaAsync(
            connectionId,
            itemType == "issues" ? SearchConnectorService.IssuesSchema : SearchConnectorService.ReposSchema);
        Console.WriteLine("Schema registered successfully.");
    }
    catch (ODataError oDataError)
    {
        Console.WriteLine($"Error registering schema: {oDataError.ResponseStatusCode}");
        Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
    }
    catch (ServiceException ex)
    {
        Console.WriteLine($"Error registering schema: {ex.ResponseStatusCode}");
        Console.WriteLine($"{ex.Message}");
    }
}

static async Task PushItemsInteractivelyAsync(
    SearchConnectorService connectorService, RepositoryService repoService, string? connectionId)
{
    _ = connectionId ?? throw new ArgumentNullException(nameof(connectionId));

    var itemType = Interactive.PromptForItemType();

    if (itemType == "issues")
    {
        await PushAllIssuesWithActivitiesAsync(connectorService, repoService, connectionId);
    }
    else
    {
        await PushAllRepositoriesAsync(connectorService, repoService, connectionId);
    }
}

static async Task PushAllIssuesWithActivitiesAsync(
    SearchConnectorService connectorService, RepositoryService repoService, string connectionId)
{
    IReadOnlyList<Issue>? issues = null;
    try
    {
        issues = await repoService.GetIssuesForRepositoryAsync();
    }
    catch (ApiException ex)
    {
        Console.WriteLine($"Error getting issues: {ex.Message}");
    }

    Console.WriteLine($"Found {issues?.Count} issues to push.");

    using var httpClient = new HttpClient();
    foreach (var issue in issues ?? new List<Issue>())
    {
        IReadOnlyList<TimelineEventInfo>? events = null;
        try
        {
            events = await repoService.GetEventsForIssueWithRetryAsync(issue.Number, 3);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting events for issue #{issue.Number}: {ex.Message}");
        }

        events = events ?? new List<TimelineEventInfo>();
        var issueItem = await issue.ToExternalItem(events, connectorService);

        // Read the HTML content for the issue, use this to
        // set the content for the item.
        var response = await httpClient.GetAsync(issue.HtmlUrl);
        if (response.IsSuccessStatusCode)
        {
            issueItem.Content = new()
            {
                Type = ExternalItemContentType.Html,
                Value = await response.Content.ReadAsStringAsync(),
            };
        }

        try
        {
            Console.Write($"Adding/updating issue {issue.Number}...");
            await connectorService.AddOrUpdateItemAsync(connectionId, issueItem);

            // Add activities from timeline events
            var activities = await events.ToExternalActivityList(connectorService);
            await connectorService.AddIssueActivitiesAsync(connectionId, issue.Number.ToString(), activities);
            Console.WriteLine("DONE");
        }
        catch (ODataError oDataError)
        {
            Console.WriteLine($"Error adding/updating issue: {oDataError.ResponseStatusCode}");
            Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
        }
    }
}

static async Task PushAllRepositoriesAsync(
    SearchConnectorService connectorService, RepositoryService repoService, string connectionId)
{
    IReadOnlyList<Repository>? repositories = null;

    try
    {
        repositories = await repoService.GetRepositoriesAsync();
    }
    catch (ApiException ex)
    {
        Console.WriteLine($"Error getting repositories: {ex.Message}");
    }

    Console.WriteLine($"Found {repositories?.Count} repos to push.");

    using var httpClient = new HttpClient();
    foreach (var repository in repositories ?? new List<Repository>())
    {
        IReadOnlyList<Activity>? events = null;
        try
        {
            events = await repoService.GetEventsForRepoWithRetryAsync(repository.Id, 3);
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"Error getting events for ${repository.Name}: {ex.Message}");
        }

        var repoItem = await repository.ToExternalItem(events, connectorService);

        if (repository.Visibility == RepositoryVisibility.Public)
        {
            // Get the README for the repo
            var readme = await repoService.GetReadmeAsync(repository);
            if (readme != null)
            {
                repoItem.Content = new()
                {
                    Type = ExternalItemContentType.Text,
                    Value = Markdig.Markdown.ToPlainText(readme.Content),
                };
            }
        }
        else
        {
            // Set content to the JSON representation
            repoItem.Content = new()
            {
                Type = ExternalItemContentType.Text,
                Value = System.Text.Json.JsonSerializer.Serialize(repository),
            };
        }

        try
        {
            Console.Write($"Adding/updating repository {repository.Name}...");
            await connectorService.AddOrUpdateItemAsync(connectionId, repoItem);
            Console.WriteLine("DONE");
        }
        catch (ODataError oDataError)
        {
            Console.WriteLine($"Error adding/updating repository: {oDataError.ResponseStatusCode}");
            Console.WriteLine($"{oDataError.Error?.Code}: {oDataError.Error?.Message}");
        }
    }
}
