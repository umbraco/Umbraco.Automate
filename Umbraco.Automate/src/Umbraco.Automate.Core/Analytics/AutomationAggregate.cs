namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// One automation's run figures for a window.
/// </summary>
/// <param name="AutomationId">The automation's ID.</param>
/// <param name="Name">The automation's current name.</param>
/// <param name="WorkspaceId">The automation's current workspace.</param>
/// <param name="Runs">All runs that started in the window.</param>
/// <param name="Succeeded">Completed plus rejected runs. A rejection is a person saying no, not a fault.</param>
/// <param name="Failed">Failed runs.</param>
/// <param name="Finished">Completed, rejected, cancelled and failed runs.</param>
/// <param name="AvgDurationMs">Average duration of finished runs with a recorded duration; null when none.</param>
/// <param name="LastStartedUtc">When the most recent run started.</param>
internal sealed record AutomationAggregate(
    Guid AutomationId,
    string Name,
    Guid WorkspaceId,
    int Runs,
    int Succeeded,
    int Failed,
    int Finished,
    double? AvgDurationMs,
    DateTime LastStartedUtc);
