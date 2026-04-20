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
    /// Gets or sets the message to show in the toast.
    /// </summary>
    [Field(Label = "Message", Description = "The message to show the editor.", SortOrder = 1, SupportsBindings = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the notification severity.
    /// </summary>
    [Field(Label = "Severity", Description = "Severity of the notification — controls the toast colour.", SortOrder = 2)]
    public EditorNotificationSeverity Severity { get; set; } = EditorNotificationSeverity.Default;
}
