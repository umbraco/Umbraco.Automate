namespace Umbraco.Automate.OpenIddict.ConnectionTypes;

/// <summary>
/// Non-generic surface for <see cref="OAuthConnectionTypeBase{TSettings}"/>, so callers (e.g. the
/// OAuth provider status endpoint) can find an OAuth connection type by provider name via the
/// connection type catalogue without needing the settings type parameter.
/// </summary>
public interface IOAuthConnectionType
{
    /// <summary>
    /// Gets the OpenIddict provider name as defined in the WebIntegration registration
    /// (e.g. <c>"Slack"</c>, <c>"Google"</c>).
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets an optional URL to documentation describing how to obtain this provider's OAuth
    /// application credentials (client ID/secret) — e.g. the provider's app-registration page.
    /// Surfaced in the backoffice when the provider's credentials are not yet configured.
    /// </summary>
    string? SetupDocsUrl { get; }
}
