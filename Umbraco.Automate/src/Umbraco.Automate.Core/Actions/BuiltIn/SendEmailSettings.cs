using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SendEmailAction"/>.
/// </summary>
public sealed class SendEmailSettings
{
    /// <summary>
    /// Gets or sets the recipient email addresses (comma-separated).
    /// </summary>
    [Field(Label = "To", Description = "Recipient email addresses, separated by commas.", SupportsBindings = true)]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the address(es) replies should go to (comma-separated). Optional; when empty
    /// no Reply-To header is set and replies go to the sender, as before.
    /// </summary>
    /// <remarks>
    /// Typically bound to visitor input (e.g. <c>${ trigger.email }</c> on a form notification),
    /// so an address that does not parse is skipped rather than failing the step.
    /// </remarks>
    [Field(Label = "Reply-To", Description = "Optional. Address(es) replies should go to, separated by commas.",
        SortOrder = 1, SupportsBindings = true)]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the email subject line.
    /// </summary>
    [Field(Label = "Subject", Description = "The email subject line.", SortOrder = 2, SupportsBindings = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email body. Supports HTML.
    /// </summary>
    [Field(Label = "Body", Description = "The email body content. HTML is supported.",
        SortOrder = 3, SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "html" },
                { "alias": "height", "value": 250 },
                { "alias": "wordWrap", "value": true }
            ]
            """)]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the body is HTML. Defaults to true.
    /// </summary>
    [Field(Label = "HTML Body", Description = "Whether the body contains HTML.",
        SortOrder = 4,
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool IsHtml { get; set; } = true;
}
