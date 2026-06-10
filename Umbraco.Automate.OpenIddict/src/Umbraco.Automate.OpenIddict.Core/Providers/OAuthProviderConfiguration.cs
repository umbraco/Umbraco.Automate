namespace Umbraco.Automate.OpenIddict.Providers;

/// <summary>
/// Configuration for an OAuth provider's application credentials.
/// These are the client ID/secret registered with the external provider (e.g. a Slack OAuth App),
/// not the user's access tokens.
/// </summary>
/// <remarks>
/// Not <c>sealed</c> so provider packages (Slack, etc.) can inherit from it when
/// shaping their appsettings JSON schema — letting them expose provider-specific
/// fields (e.g. Slack's UserScopes) alongside the shared OAuth fields.
/// </remarks>
public class OAuthProviderConfiguration
{
    /// <summary>
    /// Gets or sets the OAuth client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth client secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth scopes to request.
    /// </summary>
    public string[] Scopes { get; set; } = [];
}
