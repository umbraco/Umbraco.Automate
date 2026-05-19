using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="MemberSavedTrigger"/>.
/// </summary>
public sealed class MemberSavedTriggerSettings : IAutomationOriginatedEventBehavior
{
    /// <summary>
    /// Gets or sets the member type unique IDs to filter on (comma-separated). If null,
    /// all member types match.
    /// </summary>
    [Field(
        Label = "Member Types",
        Description = "Only fire for these member types. Leave blank to match all.",
        EditorUiAlias = "Umb.Automate.MemberTypePicker",
        SortOrder = 1)]
    public string? MemberTypes { get; set; }

    /// <summary>
    /// Gets or sets the member group unique IDs to filter on (comma-separated). If null,
    /// fires regardless of group membership. Match-if-any semantics: a member that
    /// belongs to at least one configured group fires the trigger.
    /// </summary>
    [Field(
        Label = "Member Groups",
        Description = "Only fire for members in these groups. Leave blank to match all.",
        EditorUiAlias = "Umb.PropertyEditorUi.MemberGroupPicker",
        SortOrder = 2)]
    public string? MemberGroups { get; set; }

    /// <summary>
    /// Gets or sets how the trigger should react to saves performed by another automation.
    /// Stored as a string so the dropdown picker round-trips cleanly — parsed via the
    /// <see cref="IAutomationOriginatedEventBehavior"/> implementation below.
    /// </summary>
    [Field(
        Label = "When triggered by another automation",
        Description = "How to handle saves performed by another automation.",
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """
            [{ "alias": "items", "value": [
                { "name": "Always run", "value": "Run" },
                { "name": "Skip if this would loop", "value": "SkipOnCycle" },
                { "name": "Skip entirely", "value": "SkipAlways" }
            ] }]
            """,
        Group = "Advanced",
        SortOrder = 100)]
    public string OnAutomationOriginatedEvent { get; set; } = nameof(AutomationOriginatedEventBehavior.SkipOnCycle);

    /// <inheritdoc />
    AutomationOriginatedEventBehavior IAutomationOriginatedEventBehavior.OnAutomationOriginated
        => Enum.TryParse<AutomationOriginatedEventBehavior>(OnAutomationOriginatedEvent, ignoreCase: true, out var value)
            ? value
            : AutomationOriginatedEventBehavior.SkipOnCycle;
}
