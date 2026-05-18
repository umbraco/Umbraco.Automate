using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings shared across the user auth triggers (login success, login failed, locked,
/// password changed). The set is intentionally minimal — auth events are inherently
/// system-originated, so the only configurable behaviour is loop prevention against
/// automations that themselves perform auth-related changes.
/// </summary>
public sealed class UserAuthTriggerSettings : IAutomationOriginatedEventBehavior
{
    /// <summary>
    /// Gets or sets how the trigger should react to events caused by another automation.
    /// </summary>
    [Field(
        Label = "When triggered by another automation",
        Description = "How to handle events caused by another automation.",
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
