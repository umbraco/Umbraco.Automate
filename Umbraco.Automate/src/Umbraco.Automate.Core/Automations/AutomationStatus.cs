namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// The lifecycle status of an automation.
/// </summary>
public enum AutomationStatus
{
    /// <summary>
    /// Newly created or never published. Editable, testable via dry-run.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Has a published version. Triggers evaluate against the published definition.
    /// </summary>
    Published = 1,

    /// <summary>
    /// Previously published, explicitly deactivated. Published version retained for rollback.
    /// </summary>
    Inactive = 2,
}
