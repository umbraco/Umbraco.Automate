namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The recorded trigger data of a run, prepared for display: sensitive values are masked, the JSON
/// is pretty-printed and the value is truncated to a maximum length. Carries the identifiers a
/// caller needs to authorize access. Loaded on demand (rather than as part of
/// <see cref="AutomationRun"/>) because the payload can be large, e.g. a webhook body.
/// </summary>
public sealed class RunTriggerData
{
    /// <summary>
    /// Gets the run ID.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the automation the run belongs to.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the trigger data as masked, pretty-printed and possibly truncated JSON, or <c>null</c>
    /// when the run was started without any (or the stored value could not be read, in which case
    /// it is withheld rather than shown unmasked).
    /// </summary>
    public string? TriggerData { get; init; }

    /// <summary>
    /// Gets whether <see cref="TriggerData"/> was truncated for display.
    /// </summary>
    public bool TriggerDataTruncated { get; init; }

    /// <summary>
    /// Creates the display-safe view of a stored run's trigger data.
    /// </summary>
    internal static RunTriggerData Create(StoredRunTriggerData stored, IRunDataSanitizer sanitizer)
    {
        var triggerData = sanitizer.SanitizeTriggerData(stored.TriggerData);

        return new RunTriggerData
        {
            RunId = stored.RunId,
            AutomationId = stored.AutomationId,
            TriggerData = triggerData.Value,
            TriggerDataTruncated = triggerData.Truncated,
        };
    }
}
