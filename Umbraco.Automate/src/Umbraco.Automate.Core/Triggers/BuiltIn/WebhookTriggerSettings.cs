using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers.Webhooks;

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
    /// Gets or sets the authentication strategy and its strategy-specific settings.
    /// </summary>
    [Field(
        Label = "Authentication",
        Description = "How incoming webhook requests are authenticated.",
        EditorUiAlias = "Umb.Automate.WebhookAuthenticatorPicker")]
    public WebhookAuthenticatorConfig Authenticator { get; set; } = new();
}
