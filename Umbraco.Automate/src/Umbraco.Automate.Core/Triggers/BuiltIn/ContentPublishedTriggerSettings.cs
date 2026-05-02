using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="ContentPublishedTrigger"/>.
/// </summary>
public sealed class ContentPublishedTriggerSettings : IAutomationOriginatedEventBehavior
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
    /// Gets or sets how the trigger should react to publishes performed by another automation.
    /// Stored as a string so the dropdown picker round-trips cleanly — parsed via the
    /// <see cref="IAutomationOriginatedEventBehavior"/> implementation below.
    /// </summary>
    [Field(
        Label = "When triggered by another automation",
        Description = "How to handle publishes performed by another automation.",
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
