// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Octokit;

namespace GitHubConnector.Services;

/// <summary>
/// Contains methods to get data from GitHub.
/// </summary>
public class RepositoryService
{
    private readonly GitHubClient gitHubClient;
    private readonly string gitHubOwner;
    private readonly string gitHubRepo;

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryService"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <exception cref="ArgumentException">Thrown if required settings are not contained in the settings argument.</exception>
    public RepositoryService(AppSettings settings)
    {
        gitHubOwner = !string.IsNullOrEmpty(settings.GitHubRepoOwner) ?
            settings.GitHubRepoOwner : throw new ArgumentException("gitHubRepoOwner not set in app settings");
        gitHubRepo = !string.IsNullOrEmpty(settings.GitHubRepo) ?
            settings.GitHubRepo : throw new ArgumentException("gitHubRepo not set in app settings");

        gitHubClient = new GitHubClient(new ProductHeaderValue("GitHubIssueConnector", "1.0"))
        {
            Credentials = new Credentials(settings.GitHubToken),
        };
    }

    /// <summary>
    /// Gets a list of repositories for the user or organization specified in app settings.
    /// </summary>
    /// <returns>The list of repositories.</returns>
    public async Task<IReadOnlyList<Repository>> GetRepositoriesAsync()
    {
        try
        {
            // Assume owner is an organization
            return await gitHubClient.Repository.GetAllForOrg(gitHubOwner);
        }
        catch (NotFoundException)
        {
            // If not found as an organization, try as a user
            return await gitHubClient.Repository.GetAllForUser(gitHubOwner);
        }
    }

    /// <summary>
    /// Gets a list of issues for the repository specified in app settings.
    /// </summary>
    /// <returns>The list of issues.</returns>
    public Task<IReadOnlyList<Issue>> GetIssuesForRepositoryAsync()
    {
        return gitHubClient.Issue.GetAllForRepository(gitHubOwner, gitHubRepo);
    }

    /// <summary>
    /// Gets timeline events for an issue.
    /// </summary>
    /// <param name="issueNumber">The issue number of the issue.</param>
    /// <param name="retries">The number of times to retry if a rate limit error is received.</param>
    /// <returns>The list of events.</returns>
    public async Task<IReadOnlyList<TimelineEventInfo>> GetEventsForIssueWithRetryAsync(int issueNumber, int retries)
    {
        try
        {
            return await gitHubClient.Issue.Timeline.GetAllForIssue(gitHubOwner, gitHubRepo, issueNumber);
        }
        catch (RateLimitExceededException ex)
        {
            if (retries > 0)
            {
                Console.WriteLine($"Rate limit exceeded - waiting for {ex.GetRetryAfterTimeSpan().TotalSeconds} seconds. {retries} retries remaining.");
                await Task.Delay(ex.GetRetryAfterTimeSpan());
                return await GetEventsForIssueWithRetryAsync(issueNumber, --retries);
            }

            throw;
        }
    }

    /// <summary>
    /// Gets activity events for a repository.
    /// </summary>
    /// <param name="repoId">The ID of the repository.</param>
    /// <param name="retries">The number of times to retry if a rate limit error is received.</param>
    /// <returns>The list of events.</returns>
    public async Task<IReadOnlyList<Activity>> GetEventsForRepoWithRetryAsync(long repoId, int retries)
    {
        try
        {
            return await gitHubClient.Activity.Events.GetAllForRepository(repoId);
        }
        catch (RateLimitExceededException ex)
        {
            if (retries > 0)
            {
                Console.WriteLine($"Rate limit exceeded - waiting for {ex.GetRetryAfterTimeSpan().TotalSeconds} seconds. {retries} retries remaining.");
                await Task.Delay(ex.GetRetryAfterTimeSpan());
                return await GetEventsForRepoWithRetryAsync(repoId, --retries);
            }

            throw;
        }
    }
}
