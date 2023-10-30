// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Identity;
using GitHubConnector.Middleware;
using Microsoft.Graph;
using Microsoft.Graph.External.Connections.Item.Items.Item.MicrosoftGraphExternalConnectorsAddActivities;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.Kiota.Authentication.Azure;

namespace GitHubConnector.Services;

/// <summary>
/// Contains methods to manage search connections, schema, and items.
/// </summary>
public class SearchConnectorService
{
    /// <summary>
    /// Schema for ingesting GitHub issues.
    /// </summary>
    public static readonly Schema IssuesSchema = new()
    {
        BaseType = "microsoft.graph.externalItem",
        Properties = new()
        {
            new() { Aliases = new() { "issueTitle" }, Name = "title", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true,  IsRefinable = false, Labels = new() { Label.Title } },
            new() { Aliases = new() { "message" }, Name = "body", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false },
            new() { Name = "assignees", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false },
            new() { Name = "labels", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false },
            new() { Name = "state", Type = PropertyType.String, IsSearchable = false, IsQueryable = true, IsRetrievable = true, IsRefinable = true },
            new() { Name = "issueUrl", Type = PropertyType.String, IsSearchable = false, IsQueryable = false, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.Url } },
            new() { Name = "icon", Type = PropertyType.String, IsSearchable = false, IsQueryable = false, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.IconUrl } },
            new() { Name = "updatedAt", Type = PropertyType.DateTime, IsSearchable = false, IsQueryable = true, IsRetrievable = true, IsRefinable = true, Labels = new() { Label.LastModifiedDateTime } },
            new() { Name = "lastModifiedBy", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.LastModifiedBy } },
        },
    };

    /// <summary>
    /// Schema for ingesting GitHub repositories.
    /// </summary>
    public static readonly Schema ReposSchema = new()
    {
        BaseType = "microsoft.graph.externalItem",
        Properties = new()
        {
            new() { Aliases = new() { "repoName" }, Name = "title", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.Title } },
            new() { Name = "description", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false },
            new() { Name = "visibility", Type = PropertyType.String, IsSearchable = false, IsQueryable = true, IsRetrievable = true, IsRefinable = true },
            new() { Name = "createdBy", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.CreatedBy } },
            new() { Name = "updatedAt", Type = PropertyType.DateTime, IsSearchable = false, IsQueryable = true, IsRetrievable = true, IsRefinable = true, Labels = new() { Label.LastModifiedDateTime } },
            new() { Name = "lastModifiedBy", Type = PropertyType.String, IsSearchable = true, IsQueryable = true, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.LastModifiedBy } },
            new() { Name = "repoUrl", Type = PropertyType.String, IsSearchable = true, IsQueryable = false, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.Url } },
            new() { Name = "userUrl", Type = PropertyType.String, IsSearchable = true, IsQueryable = false, IsRetrievable = true, IsRefinable = false },
            new() { Name = "icon", Type = PropertyType.String, IsSearchable = false, IsQueryable = false, IsRetrievable = true, IsRefinable = false, Labels = new() { Label.IconUrl } },
        },
    };

    private readonly GraphServiceClient graphClient;
    private readonly HttpClient httpClient;
    private readonly string gitHubOwner;
    private readonly string gitHubRepo;
    private readonly string placeholderUserId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchConnectorService"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <exception cref="ArgumentException">Thrown if required settings are not contained in the settings argument.</exception>
    public SearchConnectorService(AppSettings settings)
    {
        gitHubOwner = !string.IsNullOrEmpty(settings.GitHubRepoOwner) ?
            settings.GitHubRepoOwner : throw new ArgumentException("gitHubRepoOwner not set in app settings");
        gitHubRepo = !string.IsNullOrEmpty(settings.GitHubRepo) ?
            settings.GitHubRepo : throw new ArgumentException("gitHubRepo not set in app settings");
        placeholderUserId = !string.IsNullOrEmpty(settings.PlaceholderUserId) ?
            settings.PlaceholderUserId : throw new ArgumentException("placeholderUserId not set in app settings");

        // Get the Microsoft Graph SDK
        // middlewares (retry, auth, etc.)
        var handlers = GraphClientFactory.CreateDefaultHandlers();

        // Add handler for polling for schema registration status
        handlers.Insert(0, new PollForSchemaStatusHandler());

        // Create an HttpClient with handlers
        httpClient = GraphClientFactory.Create(handlers);

        var credential = new ClientSecretCredential(
            settings.TenantId, settings.ClientId, settings.ClientSecret);

        var authProvider = new AzureIdentityAuthenticationProvider(
            credential, scopes: new[] { "https://graph.microsoft.com/.default" });

        graphClient = new GraphServiceClient(httpClient, authProvider, "https://graph.microsoft.com/beta");
    }

    /// <summary>
    /// Creates an <see cref="ExternalConnection"/>.
    /// </summary>
    /// <param name="connectionId">The connection ID for the new connection.</param>
    /// <param name="name">The display name of the new connection.</param>
    /// <param name="description">The description of the new connection.</param>
    /// <param name="itemType">The item type for the new connection (`issues` or `repos`).</param>
    /// <param name="connectorTicket">The connector ticket when creating a connection from an M365 app.</param>
    /// <param name="connectorId">The connector ID when creating a connection from an M365 app.</param>
    /// <returns>The new <see cref="ExternalConnection"/>.</returns>
    public async Task<ExternalConnection?> CreateConnectionAsync(
        string connectionId,
        string name,
        string? description,
        string itemType,
        string? connectorTicket = null,
        string? connectorId = null)
    {
        // Load the appropriate adaptive card for the search result template
        var resultCardJsonFile = itemType == "issues" ?
            "./result-cards/result-typeIssues.json" :
            "./result-cards/result-typeRepos.json";

        var newConnection = new ExternalConnection
        {
            Id = connectionId,
            Name = name,
            Description = description,
            ActivitySettings = new()
            {
                UrlToItemResolvers = new()
                {
                    new ItemIdResolver
                    {
                        Priority = 1,
                        ItemId = itemType == "issues" ? "{issueId}" : "{repo}",
                        UrlMatchInfo = new()
                        {
                            UrlPattern = itemType == "issues" ?
                                $"/{gitHubOwner}/{gitHubRepo}/issues/(?<issueId>[0-9]+)" :
                                $"/{gitHubOwner}/(?<repo>.*)/",
                            BaseUrls = new() { "https://github.com" },
                        },
                    },
                },
            },
            SearchSettings = new()
            {
                SearchResultTemplates = new()
                {
                    new()
                    {
                        Id = itemType == "issues" ? "issueDisplay" : "repoDisplay",
                        Priority = 1,
                        Layout = new()
                        {
                            AdditionalData = await GetResultTemplateAsync(resultCardJsonFile),
                        },
                    },
                },
            },
        };

        // Only send the M365 app properties (ConnectorId and GraphConnectors-Ticket header)
        // if they are both provided, otherwise API call will fail
        // See https://learn.microsoft.com/graph/connecting-external-content-deploy-teams
        var useM365Properties = !string.IsNullOrEmpty(connectorTicket) &&
            !string.IsNullOrEmpty(connectorId);

        if (useM365Properties)
        {
            newConnection.ConnectorId = connectorId;
        }

        return await graphClient.External.Connections.PostAsync(newConnection, (config) =>
        {
            if (useM365Properties)
            {
                config.Headers.Add("GraphConnectors-Ticket", new[] { connectorTicket! });
            }
        });
    }

    /// <summary>
    /// Gets existing connections.
    /// </summary>
    /// <returns>An <see cref="ExternalConnectionCollectionResponse"/> containing the existing connections.</returns>
    public Task<ExternalConnectionCollectionResponse?> GetConnectionsAsync()
    {
        return graphClient.External.Connections.GetAsync();
    }

    /// <summary>
    /// Delete a connection.
    /// </summary>
    /// <param name="connectionId">The connection ID of the connection to delete.</param>
    /// <returns>A <see cref="Task"/> indicating the status of the asynchronous delete operation.</returns>
    public Task DeleteConnectionAsync(string? connectionId)
    {
        return graphClient.External.Connections[connectionId].DeleteAsync();
    }

    /// <summary>
    /// Registers a schema for a connection.
    /// </summary>
    /// <param name="connectionId">The connection ID of the connection.</param>
    /// <param name="schema">The <see cref="Schema"/> to register.</param>
    /// <returns>A <see cref="Task"/> indicating the status of the asynchronous registration operation.</returns>
    /// <exception cref="Exception">Thrown if a native HTTP request cannot be created.</exception>
    /// <exception cref="ServiceException">Thrown if the HTTP POST request to register the schema fails.</exception>
    public async Task RegisterSchemaAsync(string connectionId, Schema schema)
    {
        // This method sends the POST request through the HttpClient,
        // rather than the Graph SDK. This is because we need access
        // to the Location header in the response. This header allows
        // us to check status of the asynchronous schema registration
        // process.
        await graphClient.External
            .Connections[connectionId]
            .Schema
            .PatchAsync(schema);
    }

    /// <summary>
    /// Adds or updates an <see cref="ExternalItem"/>.
    /// </summary>
    /// <param name="connectionId">The connection ID of the connection that contains the item.</param>
    /// <param name="item">The item to add or update.</param>
    /// <returns>The item.</returns>
    public Task<ExternalItem?> AddOrUpdateItemAsync(string connectionId, ExternalItem item)
    {
        return graphClient.External
            .Connections[connectionId]
            .Items[item.Id]
            .PutAsync(item);
    }

    /// <summary>
    /// Adds activities to an existing item.
    /// </summary>
    /// <param name="connectionId">The connection ID of the connection that contains the item.</param>
    /// <param name="itemId">The item ID of the item to update.</param>
    /// <param name="activities">The list of activities to add to the item.</param>
    /// <returns>The <see cref="AddActivitiesPostResponse"/>.</returns>
    public async Task<AddActivitiesPostResponse?> AddIssueActivitiesAsync(
        string connectionId,
        string itemId,
        List<ExternalActivity> activities)
    {
        if (activities.Count > 0)
        {
            var addActivitiesRequest = new AddActivitiesPostRequestBody
            {
                Activities = activities,
            };

            return await graphClient.External
                .Connections[connectionId]
                .Items[itemId]
                .MicrosoftGraphExternalConnectorsAddActivities
                .PostAsAddActivitiesPostResponseAsync(addActivitiesRequest);
        }

        return null;
    }

    /// <summary>
    /// Gets an <see cref="Identity"/> from a GitHub login.
    /// </summary>
    /// <param name="gitHubLogin">The GitHub login to look up.</param>
    /// <returns>The <see cref="Identity"/> that corresponds to the GitHub login.</returns>
    public Task<Identity> GetIdentityForGitHubUserAsync(string? gitHubLogin)
    {
        // This method simply returns the user ID set in
        // appsettings.json. A possible improvement would be
        // to have some mapping of GitHub logins to Azure AD users
        _ = gitHubLogin;

        return Task.FromResult(new Identity
        {
            Type = IdentityType.User,
            Id = placeholderUserId,
        });
    }

    private async Task<Dictionary<string, object>> GetResultTemplateAsync(string resultCardJsonFile)
    {
        var cardContents = await File.ReadAllTextAsync(resultCardJsonFile);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(cardContents) ??
            throw new Exception($"Could not deserialize contents of {resultCardJsonFile}");
    }
}
