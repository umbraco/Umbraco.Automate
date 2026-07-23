using Microsoft.Extensions.Configuration;

namespace Umbraco.Automate.OpenIddict.Providers;

/// <summary>
/// Reads OAuth provider credentials from <see cref="IConfiguration"/> at
/// <c>Umbraco:Automate:Providers:{providerName}</c>.
/// </summary>
/// <example>
/// appsettings.json:
/// <code>
/// {
///   "Umbraco": {
///     "Automate": {
///       "Providers": {
///         "Slack": {
///           "ClientId": "...",
///           "ClientSecret": "...",
///           "Scopes": ["chat:write", "channels:read"]
///         }
///       }
///     }
///   }
/// }
/// </code>
/// Keep the client secret out of source control — use environment variables, user secrets, or a
/// key vault to inject it at deployment time. <see cref="IConfiguration"/> reads from whichever
/// of these sources is registered, so the appsettings.json shape above is illustrative only.
/// </example>
internal sealed class ConfigurationOAuthProviderConfigurationSource : IOAuthProviderConfigurationSource
{
    private const string SectionPrefix = "Umbraco:Automate:Providers";

    private readonly IConfiguration _configuration;

    public ConfigurationOAuthProviderConfigurationSource(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public OAuthProviderConfiguration? GetConfiguration(string providerName)
    {
        var section = _configuration.GetSection($"{SectionPrefix}:{providerName}");
        if (!section.Exists())
        {
            return null;
        }

        var config = new OAuthProviderConfiguration();
        section.Bind(config);
        return config;
    }
}
