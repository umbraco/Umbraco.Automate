using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Settings for <see cref="HmacSha256WebhookAuthenticator"/>.
/// </summary>
public sealed class HmacSha256WebhookAuthenticatorSettings
{
    /// <summary>
    /// Gets or sets the HMAC signing key. The caller must send
    /// <c>X-Webhook-Signature: sha256=&lt;hex&gt;</c> computed as HMAC-SHA256(SigningKey, body).
    /// </summary>
    [Field(
        Label = "Signing Key",
        Description = "HMAC signing key. The caller must send X-Webhook-Signature: sha256=<hex> computed over the request body.",
        IsSensitive = true,
        EditorUiAlias = "Umb.Automate.WebhookSecretField")]
    public string? SigningKey { get; set; }
}
