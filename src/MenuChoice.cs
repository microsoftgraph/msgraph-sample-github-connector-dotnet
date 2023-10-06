// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace GitHubConnector;

/// <summary>
/// Represents choices from the main menu.
/// </summary>
public enum MenuChoice
{
    /// <summary>
    /// Invalid choice.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Create a new connection.
    /// </summary>
    CreateConnection,

    /// <summary>
    /// Select an existing connection.
    /// </summary>
    SelectConnection,

    /// <summary>
    /// Delete the current connection.
    /// </summary>
    DeleteConnection,

    /// <summary>
    /// Register schema on the current connection.
    /// </summary>
    RegisterSchema,

    /// <summary>
    /// Push items to the current connection.
    /// </summary>
    PushAllItems,

    /// <summary>
    /// Exit.
    /// </summary>
    Exit,
}
