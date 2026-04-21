using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers.Webhooks;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="WebhookTrigger"/>.
/// </summary>
public sealed class WebhookTriggerSettings
{
    /// <summary>
    /// Gets or sets the HTTP method this webhook accepts (e.g. "POST", "GET").
    /// Defaults to POST.
    /// </summary>
    [Field(
        Label = "Allowed Method",
        Description = "HTTP method this webhook accepts.",
        EditorUiAlias = "Umb.Automate.WebhookMethodPicker")]
    public string AllowedMethod { get; set; } = "POST";

    /// <summary>
    /// Gets or sets the authentication strategy and its strategy-specific settings.
    /// </summary>
    [Field(
        Label = "Authentication",
        Description = "How incoming webhook requests are authenticated.",
        EditorUiAlias = "Umb.Automate.WebhookAuthenticatorPicker")]
    public WebhookAuthenticatorConfig Authenticator { get; set; } = new();
}
