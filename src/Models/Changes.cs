// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace GitHubConnector.Models;

/// <summary>
/// Represents a collection of change notifications sent by Microsoft Graph.
/// This class is a workaround for https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet/issues/730.
/// </summary>
public class Changes : IAdditionalDataHolder, IParsable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Changes"/> class.
    /// </summary>
    public Changes()
    {
        AdditionalData = new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets or sets a dictionary of fields not mapped to the class.
    /// </summary>
    public IDictionary<string, object> AdditionalData { get; set; }

    /// <summary>
    /// Gets or sets a list of change notifications contained in the collection.
    /// </summary>
    public List<ChangeNotification>? Value { get; set; }

    /// <summary>
    /// Gets or sets a list of validation tokens.
    /// </summary>
    public List<string>? ValidationTokens { get; set; }

    /// <summary>
    /// Creates an instance of the <see cref="Changes"/> class.
    /// </summary>
    /// <param name="parseNode">The node to parse.</param>
    /// <returns>An instance of the <see cref="Changes"/> class.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the parseNode parameter is null.</exception>
    public static Changes CreateFromDiscriminatorValue(IParseNode parseNode)
    {
        _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
        return new Changes();
    }

    /// <inheritdoc/>
    public IDictionary<string, Action<IParseNode>> GetFieldDeserializers()
    {
        return new Dictionary<string, Action<IParseNode>>()
        {
            {
                "validationTokens", n =>
                {
                    ValidationTokens = n.GetCollectionOfPrimitiveValues<string>()?.ToList();
                }
            },
            {
                "value", n =>
                {
                    Value = n.GetCollectionOfObjectValues<ChangeNotification>(ChangeNotification.CreateFromDiscriminatorValue)?.ToList();
                }
            },
        };
    }

    /// <inheritdoc/>
    public void Serialize(ISerializationWriter writer)
    {
        throw new NotImplementedException();
    }
}
