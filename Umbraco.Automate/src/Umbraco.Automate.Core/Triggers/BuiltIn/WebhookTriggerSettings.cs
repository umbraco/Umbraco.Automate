using Umbraco.Automate.Core.Settings;

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
    /// When <see cref="ValidateSignature"/> is false (default), validated via the
    /// <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter as a plain token.
    /// When true, the secret is used as an HMAC-SHA256 key and the caller must send
    /// the body signature in the <c>X-Webhook-Signature</c> header as <c>sha256=&lt;hex&gt;</c>.
    /// Auto-generated on first save if empty.
    /// </summary>
    [Field(Label = "Webhook Secret", Description = "Secret token for authenticating incoming requests. Auto-generated if left empty.", IsSensitive = true)]
    public string? Secret { get; set; }

    /// <summary>
    /// Gets or sets whether to use HMAC-SHA256 signature validation instead of plain secret comparison.
    /// When enabled, the caller must compute <c>HMAC-SHA256(secret, body)</c> and send it in the
    /// <c>X-Webhook-Signature</c> header as <c>sha256=&lt;hex&gt;</c>.
    /// </summary>
    [Field(Label = "Validate Signature", Description = "Use HMAC-SHA256 body signing instead of a plain secret header.")]
    public bool ValidateSignature { get; set; }

    /// <summary>
    /// Gets or sets the alias of a custom <see cref="Webhooks.IWebhookAuthenticator"/> to use
    /// instead of the built-in plain-secret / HMAC-SHA256 authentication.
    /// When set, <see cref="ValidateSignature"/> is ignored and the named authenticator handles all validation.
    /// </summary>
    [Field(Label = "Authentication Strategy", Description = "Optional custom authenticator alias (e.g. 'github', 'stripe'). Leave empty to use the built-in secret/signature validation.")]
    public string? AuthenticatorAlias { get; set; }
}
