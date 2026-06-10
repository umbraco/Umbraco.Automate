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
    /// Gets or sets the email subject line.
    /// </summary>
    [Field(Label = "Subject", Description = "The email subject line.", SortOrder = 1, SupportsBindings = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email body. Supports HTML.
    /// </summary>
    [Field(Label = "Body", Description = "The email body content. HTML is supported.",
        SortOrder = 2, SupportsBindings = true,
        EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the body is HTML. Defaults to true.
    /// </summary>
    [Field(Label = "HTML Body", Description = "Whether the body contains HTML.",
        SortOrder = 3,
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool IsHtml { get; set; } = true;
}
