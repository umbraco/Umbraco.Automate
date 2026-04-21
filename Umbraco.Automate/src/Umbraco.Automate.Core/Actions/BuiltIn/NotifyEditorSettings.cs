using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="NotifyEditorAction"/>.
/// </summary>
public sealed class NotifyEditorSettings
{
    /// <summary>
    /// Gets or sets the key (GUID) of the content item whose editor should be notified.
    /// </summary>
    [Field(Label = "Content Key", Description = "The key of the content item whose editor should be notified.", SupportsBindings = true)]
    public string ContentKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the toast headline. When empty, the triggering automation's name is used.
    /// </summary>
    [Field(Label = "Title", Description = "Headline shown on the toast. Defaults to the automation name when left blank.", SortOrder = 1, SupportsBindings = true)]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the toast body. Optional — the toast shows the title alone when empty.
    /// </summary>
    [Field(Label = "Message", Description = "Body shown on the toast. Optional — when empty only the title is shown.", SortOrder = 2, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets the notification severity. Stored as a string so the dropdown picker
    /// round-trips cleanly — parsed into <see cref="EditorNotificationSeverity"/> at execute time.
    /// </summary>
    [Field(
        Label = "Severity",
        Description = "Severity of the notification — controls the toast colour.",
        SortOrder = 3,
        EditorUiAlias = "Umb.Automate.EditorNotificationSeverityPicker")]
    public string Severity { get; set; } = nameof(EditorNotificationSeverity.Default);
}
