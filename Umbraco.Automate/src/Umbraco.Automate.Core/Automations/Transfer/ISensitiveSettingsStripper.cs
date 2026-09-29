namespace Umbraco.Automate.Core.Automations.Transfer;

/// <summary>
/// Strips sensitive settings (those marked <c>[IsSensitive]</c> via EditableModelSchema)
/// from triggers, step configurations and notification channels. Used when an automation leaves the system —
/// via the in-app export flow or the Deploy integration — so credentials never land in
/// portable artifacts.
/// </summary>
public interface ISensitiveSettingsStripper
{
    /// <summary>
    /// Returns a copy of the trigger with sensitive settings removed, or the original
    /// instance when nothing needed to be stripped. Returns <c>null</c> if the input is null.
    /// </summary>
    TriggerConfiguration? StripTrigger(TriggerConfiguration? trigger);

    /// <summary>
    /// Returns a new list of step configurations with sensitive settings removed from each step.
    /// </summary>
    IList<StepConfiguration> StripSteps(IEnumerable<StepConfiguration> steps);

    /// <summary>
    /// Returns a filtered copy of the settings dictionary with sensitive fields removed,
    /// or the original instance when nothing needed to be stripped.
    /// </summary>
    Dictionary<string, object?> StripStepSettings(string actionAlias, Dictionary<string, object?> settings);

    /// <summary>
    /// Returns a filtered copy of a connection's settings dictionary with sensitive fields
    /// removed (per the connection type's settings schema), or the original instance when
    /// nothing needed to be stripped.
    /// </summary>
    Dictionary<string, object?> StripConnectionSettings(string connectionTypeAlias, Dictionary<string, object?> settings);

    /// <summary>
    /// Returns a copy of the notification settings with sensitive fields removed from each
    /// channel (per the channel's settings schema), or the original instance when there are
    /// no channels. Returns <c>null</c> if the input is null.
    /// </summary>
    /// <remarks>
    /// The default implementation returns <c>null</c>, so an implementation written before this
    /// member existed drops notification settings rather than exporting channel secrets.
    /// </remarks>
    AutomationNotificationSettings? StripNotificationSettings(AutomationNotificationSettings? notificationSettings)
        => null;
}
