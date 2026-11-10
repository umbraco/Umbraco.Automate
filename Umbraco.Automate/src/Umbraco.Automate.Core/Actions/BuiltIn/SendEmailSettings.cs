using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="SendEmailAction"/>.
/// </summary>
public sealed class SendEmailSettings
{
    // Shows each address as a tag, as an email client does, and stores them comma-separated.
    private const string EmailAddressInput = "UmbracoAutomate.PropertyEditorUi.EmailAddressInput";

    /// <summary>
    /// Gets or sets the recipient email addresses (comma-separated).
    /// </summary>
    [Field(Label = "To", Description = "Recipient email addresses.", SupportsBindings = true,
        EditorUiAlias = EmailAddressInput)]
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the address(es) replies should go to (comma-separated). Optional; when empty
    /// no Reply-To header is set and replies go to the sender, as before.
    /// </summary>
    /// <remarks>
    /// Typically bound to visitor input (e.g. <c>${ trigger.email }</c> on a form notification),
    /// so an address that does not parse is skipped rather than failing the step.
    /// </remarks>
    [Field(Label = "Reply-To", Description = "Optional. Where replies should go. An address that can't be parsed is skipped with a warning.",
        SortOrder = 1, SupportsBindings = true, EditorUiAlias = EmailAddressInput)]
    public string? ReplyTo { get; set; }

    /// <summary>
    /// Gets or sets the addresses that receive a copy (comma-separated). Optional.
    /// </summary>
    /// <remarks>
    /// Usually typed by the editor, so an address that does not parse fails the step instead of
    /// being dropped.
    /// </remarks>
    [Field(Label = "CC", Description = "Optional. Addresses that receive a copy. The step fails if one can't be parsed.",
        SortOrder = 2, SupportsBindings = true, EditorUiAlias = EmailAddressInput)]
    public string? Cc { get; set; }

    /// <summary>
    /// Gets or sets the addresses that receive a hidden copy (comma-separated). Optional.
    /// </summary>
    /// <remarks>
    /// Usually typed by the editor, so an address that does not parse fails the step instead of
    /// being dropped.
    /// </remarks>
    [Field(Label = "BCC", Description = "Optional. Addresses that receive a hidden copy. The step fails if one can't be parsed.",
        SortOrder = 3, SupportsBindings = true, EditorUiAlias = EmailAddressInput)]
    public string? Bcc { get; set; }

    /// <summary>
    /// Gets or sets the email subject line.
    /// </summary>
    [Field(Label = "Subject", Description = "The email subject line.", SortOrder = 4, SupportsBindings = true)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email body. Supports HTML.
    /// </summary>
    [Field(Label = "Body", Description = "The email body content. HTML is supported.",
        SortOrder = 5, SupportsBindings = true,
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
        SortOrder = 6,
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle")]
    public bool IsHtml { get; set; } = true;
}
