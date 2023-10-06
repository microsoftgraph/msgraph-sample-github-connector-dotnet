// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure;
using GitHubConnector.Services;
using Microsoft.Graph.Beta.Models.ExternalConnectors;
using Octokit;

namespace GitHubConnector.Extensions;

/// <summary>
/// Static class providing extensions to the <see cref="Repository"/> class.
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Creates an <see cref="ExternalItem"/> from properties of the <see cref="Repository"/>.
    /// </summary>
    /// <param name="repository">The <see cref="Repository"/> to create from.</param>
    /// <param name="events">A list of activity events for the repository.</param>
    /// <param name="connectorService">An instance of the <see cref="SearchConnectorService"/> class.</param>
    /// <returns>An instance of <see cref="ExternalItem"/>.</returns>
    public static async Task<ExternalItem> ToExternalItem(
        this Repository repository, IReadOnlyList<Activity>? events, SearchConnectorService connectorService)
    {
        return new ExternalItem
        {
            Id = repository.Id.ToString(),
            Acl = new()
            {
                new()
                {
                    Type = AclType.Everyone,
                    Value = "everyone",
                    AccessType = AccessType.Grant,
                },
            },
            Properties = repository.ToProperties(events),
            Activities = new()
            {
                new()
                {
                    OdataType = "#microsoft.graph.externalConnectors.externalActivity",
                    Type = ExternalActivityType.Created,
                    StartDateTime = repository.CreatedAt,
                    PerformedBy = await connectorService.GetIdentityForGitHubUserAsync(repository.Owner.Login),
                },
            },
        };
    }

    /// <summary>
    /// Creates a <see cref="Properties"/> from properties of the <see cref="Repository"/>.
    /// </summary>
    /// <param name="repository">The <see cref="Repository"/> to create from.</param>
    /// <param name="events">A list of activity events for the issue.</param>
    /// <returns>An instance of <see cref="Properties"/>.</returns>
    public static Properties ToProperties(this Repository repository, IReadOnlyList<Activity>? events)
    {
        string lastModifiedBy = events is not null && events.Count > 0 ?
            events[events.Count - 1].Actor.Login : repository.Owner.Login;
        return new()
        {
            AdditionalData = new Dictionary<string, object>
            {
                { "title", repository.Name },
                { "description", repository.Description },
                { "visibility", repository.Visibility?.ToString() ?? "Unknown" },
                { "createdBy", repository.Owner.Login },
                { "updatedAt", repository.UpdatedAt },
                { "lastModifiedBy", lastModifiedBy },
                { "repoUrl", repository.HtmlUrl },
                { "userUrl", repository.Owner.HtmlUrl },
                { "icon", "https://pngimg.com/uploads/github/github_PNG40.png" },
            },
        };
    }
}
