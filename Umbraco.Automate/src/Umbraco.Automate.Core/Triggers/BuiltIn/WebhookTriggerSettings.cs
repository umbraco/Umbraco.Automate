using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="WebhookTrigger"/>.
/// </summary>
public sealed class WebhookTriggerSettings
{
    /// <summary>
    /// Gets or sets the HTTP methods this webhook accepts (e.g. "POST", "PUT").
    /// Defaults to POST only.
    /// </summary>
    [Field(Label = "Allowed Methods", Description = "HTTP methods this webhook accepts.")]
    public List<string> AllowedMethods { get; set; } = ["POST"];

    /// <summary>
    /// Gets or sets the secret used to authenticate incoming webhook requests.
    /// The selected <see cref="AuthenticatorAlias"/> determines how the secret is validated
    /// (plain token, HMAC key, provider-specific, etc.). The UI pre-fills a fresh secret when
    /// empty; the server also auto-generates on save as a safety net for API-direct writes.
    /// </summary>
    [Field(
        Label = "Webhook Secret",
        Description = "Secret used to authenticate incoming requests.",
        IsSensitive = true,
        EditorUiAlias = "Umb.Automate.WebhookSecretField")]
    public string? Secret { get; set; }

    /// <summary>
    /// Gets or sets the alias of the <see cref="Webhooks.IWebhookAuthenticator"/> that validates
    /// incoming requests. Defaults to the built-in plain-secret header/query authenticator.
    /// Unknown or missing aliases fall back to <c>plain-secret</c> defensively.
    /// </summary>
    [Field(
        Label = "Authentication Strategy",
        Description = "How incoming webhook requests are authenticated.",
        EditorUiAlias = "Umb.Automate.WebhookAuthenticatorPicker")]
    public string AuthenticatorAlias { get; set; } = PlainSecretWebhookAuthenticator.WellKnownAlias;
}
