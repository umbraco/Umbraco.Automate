namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The recorded input and output of a single step run, prepared for display: sensitive values are
/// masked, the JSON is pretty-printed and each value is truncated to a maximum length. Carries the
/// identifiers a caller needs to authorize access. Loaded on demand (rather than as part of
/// <see cref="AutomationRun"/>) because the payloads can be large — an output above the inline
/// threshold is offloaded from the workflow data and lives only on the step run record.
/// </summary>
public sealed class StepRunData
{
    /// <summary>
    /// Gets the parent run ID.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the step run ID.
    /// </summary>
    public required Guid StepRunId { get; init; }

    /// <summary>
    /// Gets the automation the parent run belongs to.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the action alias that was executed.
    /// </summary>
    public required string ActionAlias { get; init; }

    /// <summary>
    /// Gets the step's input — its resolved settings — as masked, pretty-printed and possibly
    /// truncated JSON, or <c>null</c> when none was recorded (or the stored value could not be read,
    /// in which case it is withheld rather than shown unmasked).
    /// </summary>
    public string? Input { get; init; }

    /// <summary>
    /// Gets whether <see cref="Input"/> was truncated for display.
    /// </summary>
    public bool InputTruncated { get; init; }

    /// <summary>
    /// Gets the step's output as masked, pretty-printed and possibly truncated JSON, or <c>null</c>
    /// when the step produced none (or the stored value could not be read, in which case it is
    /// withheld rather than shown unmasked).
    /// </summary>
    public string? Output { get; init; }

    /// <summary>
    /// Gets whether <see cref="Output"/> was truncated for display.
    /// </summary>
    public bool OutputTruncated { get; init; }

    /// <summary>
    /// Creates the display-safe view of a stored step run's payloads.
    /// </summary>
    internal static StepRunData Create(StoredStepRunData stored, IRunDataSanitizer sanitizer)
    {
        var input = sanitizer.SanitizeStepInput(stored.ActionAlias, stored.InputData);
        var output = sanitizer.SanitizeStepOutput(stored.ActionAlias, stored.OutputData);

        return new StepRunData
        {
            RunId = stored.RunId,
            StepRunId = stored.StepRunId,
            AutomationId = stored.AutomationId,
            ActionAlias = stored.ActionAlias,
            Input = input.Value,
            InputTruncated = input.Truncated,
            Output = output.Value,
            OutputTruncated = output.Truncated,
        };
    }
}
