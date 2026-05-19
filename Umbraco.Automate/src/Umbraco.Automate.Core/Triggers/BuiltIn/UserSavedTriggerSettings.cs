using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="UserSavedTrigger"/>.
/// </summary>
public sealed class UserSavedTriggerSettings : IAutomationOriginatedEventBehavior
{
    /// <summary>
    /// Gets or sets the user group unique IDs to filter on (comma-separated). If null,
    /// fires regardless of group membership. Match-if-any semantics: a user that belongs
    /// to at least one configured group fires the trigger.
    /// </summary>
    [Field(
        Label = "User Groups",
        Description = "Only fire for users in these groups. Leave blank to match all.",
        EditorUiAlias = "Umb.Automate.UserGroupPicker")]
    public string? UserGroups { get; set; }

    /// <summary>
    /// Gets or sets how the trigger should react to saves performed by another automation.
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
        Group = "Advanced")]
    public string OnAutomationOriginatedEvent { get; set; } = nameof(AutomationOriginatedEventBehavior.SkipOnCycle);

    /// <inheritdoc />
    AutomationOriginatedEventBehavior IAutomationOriginatedEventBehavior.OnAutomationOriginated
        => Enum.TryParse<AutomationOriginatedEventBehavior>(OnAutomationOriginatedEvent, ignoreCase: true, out var value)
            ? value
            : AutomationOriginatedEventBehavior.SkipOnCycle;
}
