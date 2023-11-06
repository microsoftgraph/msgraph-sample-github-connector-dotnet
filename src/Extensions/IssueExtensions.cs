// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GitHubConnector.Services;
using Microsoft.Graph.Models.ExternalConnectors;
using Octokit;

namespace GitHubConnector.Extensions;

/// <summary>
/// Static class providing extensions to the <see cref="Issue"/> class.
/// </summary>
public static class IssueExtensions
{
    /// <summary>
    /// Creates an <see cref="ExternalItem"/> from properties of the <see cref="Issue"/>.
    /// </summary>
    /// <param name="issue">The <see cref="Issue"/> to create from.</param>
    /// <param name="events">A list of time line events for the issue.</param>
    /// <param name="connectorService">An instance of the <see cref="SearchConnectorService"/> class.</param>
    /// <returns>An instance of <see cref="ExternalItem"/>.</returns>
    public static async Task<ExternalItem> ToExternalItem(
        this Issue issue, IReadOnlyList<TimelineEventInfo>? events, SearchConnectorService connectorService)
    {
        return new ExternalItem
        {
            Id = issue.Number.ToString(),
            Acl = new()
            {
                new()
                {
                    Type = AclType.Everyone,
                    Value = "everyone",
                    AccessType = AccessType.Grant,
                },
            },
            Properties = issue.ToProperties(events),
            Activities = new()
            {
                new()
                {
                    OdataType = "#microsoft.graph.externalConnectors.externalActivity",
                    Type = ExternalActivityType.Created,
                    StartDateTime = issue.CreatedAt,
                    PerformedBy = await connectorService.GetIdentityForGitHubUserAsync(issue.User.Login),
                },
            },
        };
    }

    /// <summary>
    /// Creates a <see cref="Properties"/> from properties of the <see cref="Issue"/>.
    /// </summary>
    /// <param name="issue">The <see cref="Issue"/> to create from.</param>
    /// <param name="events">A list of time line events for the issue.</param>
    /// <returns>An instance of <see cref="Properties"/>.</returns>
    public static Properties ToProperties(this Issue issue, IReadOnlyList<TimelineEventInfo>? events)
    {
        string lastModifiedBy = events is not null && events.Count > 0 ?
            events[events.Count - 1].Actor?.Login ?? issue.User.Login : issue.User.Login;
        return new()
        {
            AdditionalData = new Dictionary<string, object>
            {
                { "title", issue.Title },
                { "body", issue.Body },
                { "assignees", AssigneesToString(issue.Assignees) },
                { "labels", LabelsToString(issue.Labels) },
                { "state", issue.State.ToString() },
                { "issueUrl", issue.HtmlUrl },
                { "icon", "https://pngimg.com/uploads/github/github_PNG40.png" },
                { "updatedAt", issue.UpdatedAt ?? DateTimeOffset.MinValue },
                { "lastModifiedBy", lastModifiedBy },
            },
        };
    }

    private static string LabelsToString(IReadOnlyList<Octokit.Label> labels)
    {
        if (labels.Count <= 0)
        {
            return "None";
        }

        var labelNames = labels.Select(l => l.Name);
        return string.Join(",", labelNames);
    }

    private static string AssigneesToString(IReadOnlyList<User> users)
    {
        if (users.Count <= 0)
        {
            return "None";
        }

        var userNames = users.Select(u => u.Login);
        return string.Join(",", userNames);
    }
}
