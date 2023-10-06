// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GitHubConnector.Services;
using Microsoft.Graph.Beta.Models.ExternalConnectors;
using Octokit;

namespace GitHubConnector.Extensions;

/// <summary>
/// Static class providing extensions to the <see cref="TimelineEventInfoExtensions"/> class.
/// </summary>
public static class TimelineEventInfoExtensions
{
    /// <summary>
    /// Creates a list of <see cref="ExternalActivity"/> items from a list of <see cref="TimelineEventInfo"/> items.
    /// </summary>
    /// <param name="events">The list of <see cref="TimelineEventInfo"/>.</param>
    /// <param name="connectorService">An instance of the <see cref="SearchConnectorService"/> class.</param>
    /// <returns>A list of <see cref="ExternalActivity"/>.</returns>
    public static async Task<List<ExternalActivity>> ToExternalActivityList(
        this IReadOnlyList<TimelineEventInfo> events, SearchConnectorService connectorService)
    {
        var activities = new List<ExternalActivity>();

        foreach (var timelineEvent in events)
        {
            activities.Add(await timelineEvent.ToExternalActivity(connectorService));
        }

        return activities;
    }

    /// <summary>
    /// Create an <see cref="ExternalActivity"/> from properties of a <see cref="TimelineEventInfo"/>.
    /// </summary>
    /// <param name="timelineEvent">The <see cref="TimelineEventInfo"/>.</param>
    /// <param name="connectorService">An instance of the <see cref="SearchConnectorService"/> class.</param>
    /// <returns>An instance of <see cref="ExternalActivity"/>.</returns>
    public static async Task<ExternalActivity> ToExternalActivity(
        this TimelineEventInfo timelineEvent, SearchConnectorService connectorService)
    {
        return new ExternalActivity
        {
            OdataType = "#microsoft.graph.externalConnectors.externalActivity",
            Type = timelineEvent.Event.Value == EventInfoState.Commented ?
                ExternalActivityType.Commented : ExternalActivityType.Modified,
            StartDateTime = timelineEvent.CreatedAt,
            PerformedBy = await connectorService.GetIdentityForGitHubUserAsync(timelineEvent.Actor?.Login),
        };
    }
}
