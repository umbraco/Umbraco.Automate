using System.Reflection;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.ConnectionTypes;

/// <summary>
/// Base class for OAuth-based connection types that use OpenIddict Client WebIntegration.
/// Provider packages derive from this and register their OpenIddict provider in a Composer.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type containing an <c>OAuthCredentialsId</c> field.</typeparam>
public abstract class OAuthConnectionTypeBase<TSettings> : ConnectionTypeBase<TSettings>
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthConnectionTypeBase{TSettings}"/> class.
    /// </summary>
    protected OAuthConnectionTypeBase(
        ConnectionTypeInfrastructure infrastructure,
        IOAuthCredentialsService credentialsService)
        : base(infrastructure)
    {
        CredentialsService = credentialsService;
    }

    /// <summary>
    /// Gets the OpenIddict provider name as defined in the WebIntegration registration
    /// (e.g. <c>"Slack"</c>, <c>"GitHub"</c>).
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets the credentials service — exposed to derived provider types so they can perform
    /// additional provider-specific checks (e.g. calling a <c>/me</c> endpoint) on top of
    /// the base "can we resolve a valid access token" check.
    /// </summary>
    protected IOAuthCredentialsService CredentialsService { get; }

    /// <summary>
    /// Validates the OAuth credential by asking the credentials service for a valid access token
    /// (which refreshes transparently if needed). Provider packages can override to add their own
    /// follow-up check — e.g. calling the provider's identity endpoint.
    /// </summary>
    public override async Task<ConnectionValidationResult> ValidateAsync(
        object? settings,
        CancellationToken cancellationToken)
    {
        if (settings is null)
        {
            return ConnectionValidationResult.Failure("Connection settings are missing.");
        }

        if (!TryReadCredentialsId(settings, out var credentialsId))
        {
            return ConnectionValidationResult.Failure(
                $"Settings for '{Name}' do not expose an OAuthCredentialsId property.");
        }

        if (credentialsId is null || credentialsId == Guid.Empty)
        {
            return ConnectionValidationResult.Failure(
                $"No {ProviderName} account has been authenticated for this connection.");
        }

        string? token;
        try
        {
            token = await CredentialsService.GetValidAccessTokenAsync(credentialsId.Value, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConnectionValidationResult.Failure(
                $"Failed to resolve a {ProviderName} access token.",
                [ex.Message]);
        }

        if (string.IsNullOrEmpty(token))
        {
            return ConnectionValidationResult.Failure(
                $"The {ProviderName} access token is expired or revoked. Reconnect the account.");
        }

        return ConnectionValidationResult.Success(
            $"{ProviderName} access token is valid.");
    }

    /// <summary>
    /// Reads the <c>OAuthCredentialsId</c> property off the settings POCO by convention.
    /// The name matches <c>SlackConnectionSettings.OAuthCredentialsId</c> — the established
    /// shape in provider packages that derive from this base.
    /// </summary>
    private static bool TryReadCredentialsId(object settings, out Guid? credentialsId)
    {
        const string propertyName = "OAuthCredentialsId";

        var property = settings.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (property is null || property.PropertyType != typeof(Guid?))
        {
            credentialsId = null;
            return false;
        }

        credentialsId = (Guid?)property.GetValue(settings);
        return true;
    }
}
