using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="ContentSavedTrigger"/>.
/// </summary>
public sealed class ContentSavedTriggerSettings : ISkipAutomationOriginatedEvents
{
    /// <summary>
    /// Gets or sets the content type unique IDs to filter on (comma-separated). If null, all content types match.
    /// </summary>
    [Field(
        Label = "Content Types",
        Description = "Only fire for these content types. Leave blank to match all.",
        EditorUiAlias = "Umb.PropertyEditorUi.DocumentTypePicker")]
    public string? ContentTypes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether saves performed by another automation should
    /// be ignored. Defaults to <c>true</c> so an automation that updates content cannot
    /// re-trigger itself (or another listening automation in a cycle). Disable only when
    /// chaining automations across saves is intentional.
    /// </summary>
    [Field(
        Label = "Skip automation-originated saves",
        Description = "Don't fire when the save was performed by another automation. Prevents trigger loops.",
        EditorUiAlias = "Umb.PropertyEditorUi.Toggle",
        Group = "Advanced")]
    public bool SkipAutomationOriginatedEvents { get; set; } = true;
}
