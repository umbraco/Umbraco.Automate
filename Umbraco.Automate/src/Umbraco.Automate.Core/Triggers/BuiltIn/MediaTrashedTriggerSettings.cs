using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="MediaTrashedTrigger"/>.
/// </summary>
public sealed class MediaTrashedTriggerSettings : IAutomationOriginatedEventBehavior
{
    /// <summary>
    /// Gets or sets the media type unique IDs to filter on (comma-separated). If null, all media types match.
    /// </summary>
    [Field(
        Label = "Media Types",
        Description = "Only fire for these media types. Leave blank to match all.",
        EditorUiAlias = "Umb.PropertyEditorUi.MediaTypePicker")]
    public string? MediaTypes { get; set; }

    /// <summary>
    /// Gets or sets how the trigger should react to trashes performed by another automation.
    /// </summary>
    [Field(
        Label = "When triggered by another automation",
        Description = "How to handle trashes performed by another automation.",
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """
            [{ "alias": "items", "value": [
                { "name": "Always run", "value": "Run" },
                { "name": "Skip if this would loop", "value": "SkipOnCycle" },
                { "name": "Skip entirely", "value": "SkipAlways" }
            ] }]
            """,
        Group = "Advanced")]
    public string OnAutomationOriginatedEvent { get; set; } = nameof(AutomationOriginatedEventBehavior.SkipOnCycle);

    /// <inheritdoc />
    AutomationOriginatedEventBehavior IAutomationOriginatedEventBehavior.OnAutomationOriginated
        => Enum.TryParse<AutomationOriginatedEventBehavior>(OnAutomationOriginatedEvent, ignoreCase: true, out var value)
            ? value
            : AutomationOriginatedEventBehavior.SkipOnCycle;
}
