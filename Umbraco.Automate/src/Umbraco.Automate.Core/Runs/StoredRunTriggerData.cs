namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// The recorded trigger payload of a run exactly as stored, together with the identifiers needed to
/// authorize access to it. Raw payloads can hold sensitive values, so this type stays internal: the
/// public surface only exposes the sanitized <see cref="RunTriggerData"/>.
/// </summary>
internal sealed class StoredRunTriggerData
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
    /// Gets the serialised trigger data (JSON), or <c>null</c> when the run was started without any.
    /// </summary>
    public string? TriggerData { get; init; }
}
