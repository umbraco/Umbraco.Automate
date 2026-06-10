using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

/// <summary>
/// Settings for <see cref="PlainSecretWebhookAuthenticator"/>.
/// </summary>
public sealed class PlainSecretWebhookAuthenticatorSettings
{
    /// <summary>
    /// Gets or sets the shared secret expected in the <c>X-Webhook-Secret</c> header
    /// or <c>secret</c> query parameter of incoming requests.
    /// </summary>
    [Field(
        Label = "Secret",
        Description = "Shared secret sent by the caller in the X-Webhook-Secret header or ?secret= query parameter.",
        IsSensitive = true,
        EditorUiAlias = "Umb.Automate.WebhookSecretField")]
    public string? Secret { get; set; }
}
