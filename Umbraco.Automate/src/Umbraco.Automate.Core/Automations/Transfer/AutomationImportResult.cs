namespace Umbraco.Automate.Core.Automations.Transfer;

/// <summary>
/// The result of an automation import operation.
/// </summary>
public sealed class AutomationImportResult
{
    /// <summary>
    /// Gets whether the import was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the ID of the created automation, if successful.
    /// </summary>
    public Guid? AutomationId { get; init; }

    /// <summary>
    /// Gets the alias of the imported automation.
    /// </summary>
    public string? AutomationAlias { get; init; }

    /// <summary>
    /// Gets any errors that prevented the import.
    /// </summary>
    public IList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets any warnings about post-import configuration needed (e.g., sensitive fields to reconfigure).
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}
