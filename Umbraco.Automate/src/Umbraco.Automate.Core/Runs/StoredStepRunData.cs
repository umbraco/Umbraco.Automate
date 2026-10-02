namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The recorded input and output payloads of a single step run exactly as stored, together with the
/// identifiers needed to authorize access to them. Raw payloads can hold sensitive values, so this
/// type stays internal: the public surface only exposes the sanitized <see cref="StepRunData"/>.
/// </summary>
internal sealed class StoredStepRunData
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
    /// Gets the serialised input data (JSON) — the step's resolved settings — or <c>null</c>
    /// when none was recorded.
    /// </summary>
    public string? InputData { get; init; }

    /// <summary>
    /// Gets the serialised output data (JSON), or <c>null</c> when the step produced none.
    /// This is always the full output, including an output offloaded from the workflow data.
    /// </summary>
    public string? OutputData { get; init; }
}
