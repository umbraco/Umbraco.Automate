using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// Maps between <see cref="AutomationRun"/> domain model and <see cref="AutomationRunEntity"/> EF entity.
/// </summary>
internal static class AutomationRunFactory
{
    public static AutomationRun BuildDomain(AutomationRunEntity entity, IList<StepRun>? stepRuns = null)
    {
        return new AutomationRun
        {
            Id = entity.Id,
            AutomationId = entity.AutomationId,
            AutomationVersion = entity.AutomationVersion,
            WorkspaceId = entity.WorkspaceId,
            ServiceAccountKey = entity.ServiceAccountKey,
            Status = (AutomationRunStatus)entity.Status,
            StartedUtc = entity.StartedUtc,
            CompletedUtc = entity.CompletedUtc,
            TriggerData = entity.TriggerData,
            InitiatedBy = entity.InitiatedBy,
            CorrelationId = entity.CorrelationId,
            Error = entity.Error,
            StepRuns = stepRuns ?? [],
        };
    }

    public static AutomationRunEntity BuildEntity(AutomationRun run)
    {
        return new AutomationRunEntity
        {
            Id = run.Id,
            AutomationId = run.AutomationId,
            AutomationVersion = run.AutomationVersion,
            WorkspaceId = run.WorkspaceId,
            ServiceAccountKey = run.ServiceAccountKey,
            Status = (int)run.Status,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            TriggerData = run.TriggerData,
            InitiatedBy = run.InitiatedBy,
            CorrelationId = run.CorrelationId,
            Error = run.Error,
        };
    }

    public static void UpdateEntity(AutomationRunEntity entity, AutomationRun run)
    {
        entity.Status = (int)run.Status;
        entity.StartedUtc = run.StartedUtc;
        entity.CompletedUtc = run.CompletedUtc;
        entity.Error = run.Error;
    }
}
