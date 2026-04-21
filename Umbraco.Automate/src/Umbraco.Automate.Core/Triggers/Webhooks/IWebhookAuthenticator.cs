using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.Webhooks;

/// <summary>
/// Validates the authenticity of an incoming webhook request.
/// Provider packages can register custom authenticators for services like GitHub, Stripe, etc.
/// Derive from <see cref="WebhookAuthenticatorBase{TSettings}"/> and decorate with
/// <see cref="WebhookAuthenticatorAttribute"/> to register one.
/// </summary>
public interface IWebhookAuthenticator
{
    /// <summary>
    /// Gets the unique alias for this authenticator (e.g. "hmac-sha256", "github", "stripe").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets a human-readable display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional short description shown in the authentication strategy picker.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether this authenticator needs the request body during validation
    /// (e.g. HMAC body-signing). When <c>false</c>, validation runs before the body is read which
    /// lets callers fail fast with 401 for unauthorized requests on large payloads.
    /// </summary>
    bool RequiresBody { get; }

    /// <summary>
    /// Gets the CLR type of the authenticator's settings model (e.g.
    /// <c>PlainSecretWebhookAuthenticatorSettings</c>). Returns <c>null</c> when the strategy
    /// has no configuration.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the editable model schema for the authenticator's settings, or <c>null</c> when
    /// there are no settings to render.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Validates the webhook request against the supplied strategy settings.
    /// </summary>
    /// <param name="context">The incoming webhook context.</param>
    /// <param name="settings">The resolved settings for this authenticator (already typed to <see cref="SettingsType"/>), or <c>null</c> when the strategy has no settings.</param>
    /// <returns><c>true</c> if the request is authentic; otherwise <c>false</c>.</returns>
    bool Validate(WebhookAuthenticationContext context, object? settings);
}

/// <summary>
/// Base class for webhook authenticators that reads discovery metadata from the
/// <see cref="WebhookAuthenticatorAttribute"/> and auto-derives the settings schema from
/// <typeparamref name="TSettings"/>.
/// </summary>
/// <typeparam name="TSettings">The settings POCO for this authenticator.</typeparam>
public abstract class WebhookAuthenticatorBase<TSettings> : IWebhookAuthenticator
    where TSettings : class, new()
{
    private readonly WebhookAuthenticatorAttribute _attribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookAuthenticatorBase{TSettings}"/> class.
    /// </summary>
    protected WebhookAuthenticatorBase()
    {
        _attribute = GetType().GetCustomAttribute<WebhookAuthenticatorAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Webhook authenticator '{GetType().FullName}' is missing a required [WebhookAuthenticator] attribute.");
    }

    /// <inheritdoc />
    public string Alias => _attribute.Alias;

    /// <inheritdoc />
    public string Name => _attribute.Name;

    /// <inheritdoc />
    public virtual string? Description => _attribute.Description;

    /// <inheritdoc />
    public virtual bool RequiresBody => true;

    /// <inheritdoc />
    public Type? SettingsType => typeof(TSettings);

    /// <inheritdoc />
    public virtual EditableModelSchema? GetSettingsSchema()
        => EditableModelSchemaBuilder.Build(typeof(TSettings));

    /// <summary>
    /// Validates the webhook request against typed settings.
    /// </summary>
    protected abstract bool Validate(WebhookAuthenticationContext context, TSettings settings);

    bool IWebhookAuthenticator.Validate(WebhookAuthenticationContext context, object? settings)
    {
        var typed = settings switch
        {
            TSettings typedSettings => typedSettings,
            null => new TSettings(),
            _ => JsonSerializer.Deserialize<TSettings>(
                     JsonSerializer.Serialize(settings, JsonOptions.Settings),
                     JsonOptions.Settings) ?? new TSettings(),
        };

        return Validate(context, typed);
    }
}

/// <summary>
/// Context passed to <see cref="IWebhookAuthenticator.Validate"/> containing
/// the HTTP request details and body.
/// </summary>
public sealed class WebhookAuthenticationContext
{
    /// <summary>
    /// Gets the incoming HTTP request.
    /// </summary>
    public required HttpRequest Request { get; init; }

    /// <summary>
    /// Gets the raw request body, or null if no body was sent or the authenticator runs before
    /// the body is read (i.e. <see cref="IWebhookAuthenticator.RequiresBody"/> is <c>false</c>).
    /// </summary>
    public string? Body { get; init; }
}
