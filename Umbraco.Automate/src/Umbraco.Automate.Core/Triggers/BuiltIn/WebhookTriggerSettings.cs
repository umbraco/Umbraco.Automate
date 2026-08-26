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

    /// <summary>
    /// Gets or sets the request body used when the automation is run on demand, standing in
    /// for the body a real caller would send. Handed to the steps verbatim, exactly as the
    /// live endpoint hands over a real body.
    /// </summary>
    [Field(
        Label = "Test request body",
        Description = "The body to use when running this automation on demand. Sent to the steps as-is.",
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 200 },
                { "alias": "wordWrap", "value": true }
            ]
            """,
        SortOrder = 100)]
    public string? TestRequestBody { get; set; }

    /// <summary>
    /// Gets or sets header overrides used when the automation is run on demand, as a JSON
    /// object of header name to value. Layered over a default <c>Content-Type</c> of
    /// <c>application/json</c>. Leave empty to send just that default.
    /// </summary>
    [Field(
        Label = "Test request headers",
        Description = "Header overrides for on-demand runs, as a JSON object. Content-Type defaults to application/json.",
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 120 },
                { "alias": "wordWrap", "value": true }
            ]
            """,
        SortOrder = 110)]
    public string? TestRequestHeaders { get; set; }
}
