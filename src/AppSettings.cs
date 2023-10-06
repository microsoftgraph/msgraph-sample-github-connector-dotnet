// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;

namespace GitHubConnector;

/// <summary>
/// Represents the settings for the application.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Gets or sets the "Application (client) ID" of the app registration in Azure.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret of the app registration in Azure.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the "Directory (tenant) ID" of the app registration in Azure.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the GitHub user or organization.
    /// </summary>
    public string? GitHubRepoOwner { get; set; }

    /// <summary>
    /// Gets or sets the GitHub repository to ingest issues from.
    /// </summary>
    public string? GitHubRepo { get; set; }

    /// <summary>
    /// Gets or sets the fine-grained personal access token to use to connect to GitHub.
    /// </summary>
    public string? GitHubToken { get; set; }

    /// <summary>
    /// Gets or sets the port number to listen on when using a Teams app to enable/disable the connector.
    /// </summary>
    public int PortNumber { get; set; }

    /// <summary>
    /// Gets or sets the placeholder user ID to map to GitHub user logins.
    /// </summary>
    public string? PlaceholderUserId { get; set; }

    /// <summary>
    /// Loads application settings from JSON files and user secret store.
    /// </summary>
    /// <returns>The loaded settings.</returns>
    /// <exception cref="Exception">Thrown if the required settings were not able to be loaded.</exception>
    public static AppSettings LoadSettings()
    {
        // Load settings
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", true)
            .AddUserSecrets<Program>()
            .Build();

        return config.GetRequiredSection("Settings").Get<AppSettings>() ??
            throw new Exception("Could not load app settings. See README for configuration instructions.");
    }
}
