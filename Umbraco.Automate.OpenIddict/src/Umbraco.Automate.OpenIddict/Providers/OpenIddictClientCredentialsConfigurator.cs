using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Client;

namespace Umbraco.Automate.OpenIddict.Providers;

/// <summary>
/// Post-configures OpenIddict client registrations by applying credentials and redirect URIs
/// from convention. This runs after all provider packages have registered their providers
/// via <c>AddClient()</c>, patching in:
/// <list type="bullet">
///   <item>Client ID/secret from <see cref="IOAuthProviderConfigurationSource"/> (replaceable via DI)</item>
///   <item>Scopes from <see cref="IOAuthProviderConfigurationSource"/> (additive — merged with any scopes set by the provider package)</item>
///   <item>Redirect URI from convention: <c>automate/oauth/callback/{providerName}</c></item>
/// </list>
/// </summary>
internal sealed class OpenIddictClientCredentialsConfigurator : IPostConfigureOptions<OpenIddictClientOptions>
{
    private readonly IOAuthProviderConfigurationSource _configurationSource;
    private readonly ILogger<OpenIddictClientCredentialsConfigurator> _logger;

    public OpenIddictClientCredentialsConfigurator(
        IOAuthProviderConfigurationSource configurationSource,
        ILogger<OpenIddictClientCredentialsConfigurator> logger)
    {
        _configurationSource = configurationSource;
        _logger = logger;
    }

    internal const string CallbackPathPrefix = "automate/oauth/callback/";

    public void PostConfigure(string? name, OpenIddictClientOptions options)
    {
        foreach (var registration in options.Registrations)
        {
            if (string.IsNullOrEmpty(registration.ProviderName))
            {
                continue;
            }

            // Apply conventional redirect URI: automate/oauth/callback/{providerName}
            registration.RedirectUri = new Uri(
                $"{CallbackPathPrefix}{registration.ProviderName.ToLowerInvariant()}", UriKind.Relative);

            var config = _configurationSource.GetConfiguration(registration.ProviderName);
            if (config is null)
            {
                _logger.LogDebug(
                    "No OAuth configuration found for provider {ProviderName} — skipping credential patching",
                    registration.ProviderName);
                continue;
            }

            if (string.IsNullOrEmpty(config.ClientId))
            {
                _logger.LogWarning(
                    "OAuth configuration for provider {ProviderName} has no ClientId — provider will not function",
                    registration.ProviderName);
                continue;
            }

            registration.ClientId = config.ClientId;
            registration.ClientSecret = config.ClientSecret ?? string.Empty;

            // Merge configured scopes (additive — provider packages may also set scopes).
            foreach (var scope in config.Scopes)
            {
                if (!string.IsNullOrEmpty(scope) && !registration.Scopes.Contains(scope))
                {
                    registration.Scopes.Add(scope);
                }
            }

            _logger.LogDebug(
                "Applied OAuth credentials and {ScopeCount} scopes for provider {ProviderName}",
                config.Scopes.Length, registration.ProviderName);
        }
    }
}
