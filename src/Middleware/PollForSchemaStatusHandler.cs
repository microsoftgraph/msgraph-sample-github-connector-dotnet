// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Graph;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.Kiota.Abstractions.Serialization;

namespace GitHubConnector.Middleware;

/// <summary>
/// Middleware handler for asynchronous creation of connector schema.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PollForSchemaStatusHandler"/> class.
/// </remarks>
/// <param name="delay">The number of milliseconds to wait between poll requests.</param>
public partial class PollForSchemaStatusHandler(int delay = 60000) : DelegatingHandler
{
    /// <summary>
    /// Gets the delay setting in milliseconds.
    /// </summary>
    public int Delay { get; private set; } = delay;

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Patch &&
            PostSchemaRegex().IsMatch(request.RequestUri?.AbsolutePath ?? string.Empty))
        {
            return HandlePatchSchemaRequestAsync(request, cancellationToken);
        }

        if (request.Method == HttpMethod.Get &&
            GetOperationRegex().IsMatch(request.RequestUri?.AbsolutePath ?? string.Empty))
        {
            return HandleGetOperationStatusRequestAsync(request, cancellationToken);
        }

        return base.SendAsync(request, cancellationToken);
    }

    // Match URL like:
    // /external/connections/{connection-id}/operations/{operation-id}
    [GeneratedRegex("\\/external\\/connections\\/[0-9a-zA-Z]+\\/operations\\/.*", RegexOptions.IgnoreCase)]
    private static partial Regex GetOperationRegex();

    // Match URL like:
    // /external/connections/{connection-id}/schema
    [GeneratedRegex("\\/external\\/connections\\/[0-9a-zA-Z]+\\/schema", RegexOptions.IgnoreCase)]
    private static partial Regex PostSchemaRegex();

    private async Task<HttpResponseMessage> HandlePatchSchemaRequestAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // For PATCH /schema, we need to get the URL from the
        // Location header and poll that for schema registration
        // status.
        var location = response.Headers.Location;
        if (location is not null)
        {
            Console.WriteLine($"Waiting {Delay}ms to poll {location.AbsoluteUri}");
            await Task.Delay(Delay, cancellationToken);

            request.RequestUri = location;
            request.Method = HttpMethod.Get;
            request.Content = null;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(25));
            return await SendAsync(request, cts.Token);
        }

        return response;
    }

    private async Task<HttpResponseMessage> HandleGetOperationStatusRequestAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new ServiceException("Schema registration timed out while checking for status.");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            // Use Graph SDK's parsers to deserialize the body
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var responseBody = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
            var parseNode = await ParseNodeFactoryRegistry.DefaultInstance.GetRootParseNodeAsync("application/json", responseBody, cancellationToken);
            var operation = parseNode.GetObjectValue(ConnectionOperation.CreateFromDiscriminatorValue) ??
                throw new ServiceException("Could not get operation from API.");

            if (operation.Status == ConnectionOperationStatus.Inprogress)
            {
                // Schema registration is in progress, poll again
                Console.WriteLine($"Waiting {Delay}ms to poll {request.RequestUri?.AbsoluteUri}");
                await Task.Delay(Delay, cancellationToken);
                return await SendAsync(request, cancellationToken);
            }

            if (operation.Status == ConnectionOperationStatus.Failed)
            {
                throw new ServiceException(
                    operation.Error?.Message ?? "Schema registration failed.",
                    response.Headers,
                    0,
                    JsonSerializer.Serialize(operation.Error));
            }
        }

        // Registration completed, return response
        return response;
    }
}
