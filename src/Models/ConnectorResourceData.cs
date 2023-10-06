// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace GitHubConnector.Models;

/// <summary>
/// Represents the data in a Microsoft Graph notification for changes to a connector.
/// </summary>
public class ConnectorResourceData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Gets or sets the ID of the connector resource data object.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the state of the connector resource data object.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets the connector ticket of the connector resource data object.
    /// </summary>
    public string? ConnectorsTicket { get; set; }

    /// <summary>
    /// Creates new instance of the <see cref="ConnectorResourceData"/> class.
    /// </summary>
    /// <param name="data">The dictionary of fields to construct from.</param>
    /// <returns>An instance of <see cref="ConnectorResourceData"/>.</returns>
    public static ConnectorResourceData? CreateFromAdditionalData(IDictionary<string, object>? data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        return JsonSerializer.Deserialize<ConnectorResourceData>(json, JsonOptions);
    }
}
