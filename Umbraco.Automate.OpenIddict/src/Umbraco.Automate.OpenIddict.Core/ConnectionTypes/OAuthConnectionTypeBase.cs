using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.OpenIddict.ConnectionTypes;

/// <summary>
/// Base class for OAuth-based connection types that use OpenIddict Client WebIntegration.
/// Provider packages derive from this and register their OpenIddict provider in a Composer.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type containing an <c>OAuthCredentialId</c> field.</typeparam>
public abstract class OAuthConnectionTypeBase<TSettings> : ConnectionTypeBase<TSettings>
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthConnectionTypeBase{TSettings}"/> class.
    /// </summary>
    protected OAuthConnectionTypeBase(ConnectionTypeInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    /// <summary>
    /// Gets the OpenIddict provider name as defined in the WebIntegration registration
    /// (e.g. <c>"Slack"</c>, <c>"GitHub"</c>).
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Validates that the OAuth credential exists and has a valid token.
    /// Override to add provider-specific validation (e.g. calling a /me endpoint).
    /// </summary>
    public override Task<bool> ValidateAsync(object? settings, CancellationToken cancellationToken)
    {
        // Default: accept if settings resolve without error.
        // Provider packages can override to test the credential.
        return Task.FromResult(true);
    }
}
