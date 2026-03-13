using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Payload model sent to notification channels when a run fails or suspends.
/// </summary>
public sealed class RunFailureNotification
{
    /// <summary>
    /// Gets the automation ID.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the automation name.
    /// </summary>
    public required string AutomationName { get; init; }

    /// <summary>
    /// Gets the run ID.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the final run status.
    /// </summary>
    public required AutomationRunStatus Status { get; init; }

    /// <summary>
    /// Gets the error message, if any.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the workspace ID.
    /// </summary>
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the UTC time the run completed.
    /// </summary>
    public DateTime? CompletedUtc { get; init; }
}
