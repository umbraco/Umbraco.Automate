namespace Umbraco.Automate.Core.Automations.Transfer;

/// <summary>
/// Options controlling which optional sections are included in an export.
/// By default, only the core automation definition is exported.
/// </summary>
public sealed class AutomationExportOptions
{
    /// <summary>
    /// Gets or sets whether to include notification settings in the export.
    /// </summary>
    public bool IncludeNotifications { get; init; }
}
