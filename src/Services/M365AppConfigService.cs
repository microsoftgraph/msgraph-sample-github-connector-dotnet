// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using GitHubConnector.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Kiota.Serialization.Json;

namespace GitHubConnector.Services;

/// <summary>
/// Contains methods for listening for configuration changes from a Microsoft 365 app.
/// </summary>
public class M365AppConfigService
{
    private readonly HttpListener listener;
    private readonly int port;
    private readonly string tenantId;
    private readonly string clientId;

    /// <summary>
    /// Initializes a new instance of the <see cref="M365AppConfigService"/> class.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    public M365AppConfigService(AppSettings settings)
    {
        tenantId = settings.TenantId ?? throw new ArgumentException("tenantId not set in app settings");
        clientId = settings.ClientId ?? throw new ArgumentException("clientId not set in app settings");
        port = settings.PortNumber;
        listener = new();
    }

    /// <summary>
    /// Starts listening on the specified HTTP port.
    /// </summary>
    /// <returns>An instance of <see cref="HttpListener"/>.</returns>
    public HttpListener Start()
    {
        listener.Start();
        listener.Prefixes.Add($"http://localhost:{port}/");
        Console.WriteLine($"Listening on port {port}...");
        return listener;
    }

    /// <summary>
    /// Stops listening on the specified HTTP port.
    /// </summary>
    public void Stop()
    {
        listener.Stop();
    }

    /// <summary>
    /// Deserializes the body of an incoming HTTP POST request.
    /// </summary>
    /// <param name="postBody">The input <see cref="Stream"/> to deserialize.</param>
    /// <returns>An instance of the <see cref="Changes"/> class.</returns>
    public Changes? DeserializePostBody(Stream postBody)
    {
        var parseNode = new JsonParseNodeFactory().GetRootParseNode("application/json", postBody);
        return parseNode.GetObjectValue(Changes.CreateFromDiscriminatorValue);
    }

    /// <summary>
    /// Validates the tokens sent by Microsoft Graph.
    /// </summary>
    /// <param name="tokens">A list of tokens.</param>
    /// <returns>A value indicating whether the tokens are valid or not.</returns>
    public async Task<bool> ValidateTokensAsync(List<string>? tokens)
    {
        if (tokens is null)
        {
            return false;
        }

        // Get the OpenID configuration in order to get the signing keys
        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
        var openIdConfig = await configurationManager.GetConfigurationAsync();
        var handler = new JwtSecurityTokenHandler();

        foreach (var token in tokens)
        {
            try
            {
                handler.ValidateToken(
                    token,
                    new()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuers = new[]
                        {
                            $"https://login.microsoftonline.com/{tenantId}/v2.0",
                            $"https://sts.windows.net/{tenantId}/",
                        },
                        ValidAudience = clientId,
                        IssuerSigningKeys = openIdConfig.SigningKeys,
                    },
                    out _);
            }
            catch (SecurityTokenValidationException)
            {
                return false;
            }
        }

        return true;
    }
}
