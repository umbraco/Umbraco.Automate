namespace Umbraco.Automate.OpenIddict.Providers;

/// <summary>
/// Resolves OAuth provider application credentials (client ID/secret).
/// The default implementation reads from <c>Umbraco:Automate:Providers:{providerName}</c> in <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// Can be replaced to read from a database or other source.
/// </summary>
public interface IOAuthProviderConfigurationSource
{
    /// <summary>
    /// Gets the configuration for the specified OAuth provider.
    /// Returns null if the provider is not configured.
    /// </summary>
    /// <param name="providerName">The provider name (e.g. "Slack", "GitHub").</param>
    OAuthProviderConfiguration? GetConfiguration(string providerName);
}
