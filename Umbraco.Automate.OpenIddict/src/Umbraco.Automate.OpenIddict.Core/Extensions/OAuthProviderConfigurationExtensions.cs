using System.Diagnostics.CodeAnalysis;
using Umbraco.Automate.OpenIddict.Providers;

namespace Umbraco.Automate.OpenIddict.Extensions;

/// <summary>
/// Extension methods for <see cref="OAuthProviderConfiguration"/>.
/// </summary>
public static class OAuthProviderConfigurationExtensions
{
    /// <summary>
    /// Gets whether the provider has both a client ID and secret configured — the minimum needed
    /// to dispatch an OAuth challenge. Centralises the "is this provider configured?" check shared
    /// by the challenge guard and the provider-status endpoint so the two cannot drift (#107).
    /// </summary>
    public static bool HasClientCredentials([NotNullWhen(true)] this OAuthProviderConfiguration? configuration) =>
        !string.IsNullOrWhiteSpace(configuration?.ClientId)
        && !string.IsNullOrWhiteSpace(configuration?.ClientSecret);
}
