using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Persistence.Notifications;

/// <summary>
/// On application startup, marks any automation runs left in <see cref="AutomationRunStatus.Running"/>
/// or <see cref="AutomationRunStatus.Pending"/> as <see cref="AutomationRunStatus.Failed"/>.
/// These represent workflows that were in-flight when the previous process stopped.
/// Skipped on <see cref="ServerRole.Subscriber"/> nodes — subscribers must not mark runs
/// as failed that may still be executing elsewhere. Runs on all other roles including
/// <see cref="ServerRole.Unknown"/> (role election may not have completed at startup).
/// </summary>
internal sealed class StuckRunRecoveryNotificationHandler
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly int[] NonTerminalStatuses =
    [
        (int)AutomationRunStatus.Running,
        (int)AutomationRunStatus.Pending,
    ];

    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly ILogger<StuckRunRecoveryNotificationHandler> _logger;

    public StuckRunRecoveryNotificationHandler(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        IServerRoleAccessor serverRoleAccessor,
        ILogger<StuckRunRecoveryNotificationHandler> logger)
    {
        _dbContextFactory = dbContextFactory;
        _serverRoleAccessor = serverRoleAccessor;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (_serverRoleAccessor.CurrentServerRole is ServerRole.Subscriber)
        {
            _logger.LogDebug(
                "Stuck run recovery skipped — this node ({ServerRole}) is a subscriber",
                _serverRoleAccessor.CurrentServerRole);
            return;
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.UtcNow;

        // Exclude runs that have steps in a durable status (Sleeping, WaitingForInput) —
        // WorkflowCore will resume these naturally via its persistence mechanism.
        var durableStepStatuses = new[]
        {
            (int)StepRunStatus.Sleeping,
            (int)StepRunStatus.WaitingForInput,
        };

        var durableRunIds = await db.StepRuns
            .Where(sr => durableStepStatuses.Contains(sr.Status))
            .Select(sr => sr.RunId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var stuckStepStatuses = new[] { (int)StepRunStatus.Pending, (int)StepRunStatus.Running };

        // 1. Recover stuck runs and their step runs.
        var stuckRunIds = await db.AutomationRuns
            .Where(r => NonTerminalStatuses.Contains(r.Status) && !durableRunIds.Contains(r.Id))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var recoveredSteps = 0;
        var recoveredRuns = 0;

        if (stuckRunIds.Count > 0)
        {
            recoveredSteps = await db.StepRuns
                .Where(sr => stuckRunIds.Contains(sr.RunId) && stuckStepStatuses.Contains(sr.Status))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(sr => sr.Status, (int)StepRunStatus.Failed)
                        .SetProperty(sr => sr.CompletedUtc, now)
                        .SetProperty(sr => sr.Error, "Recovered after application restart — workflow was interrupted"),
                    cancellationToken);

            recoveredRuns = await db.AutomationRuns
                .Where(r => stuckRunIds.Contains(r.Id))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(r => r.Status, (int)AutomationRunStatus.Failed)
                        .SetProperty(r => r.CompletedUtc, now)
                        .SetProperty(r => r.Error, "Recovered after application restart — workflow was interrupted"),
                    cancellationToken);
        }

        // 2. Recover orphaned step runs whose parent run already reached a terminal state
        //    but the steps were left in Running/Pending (e.g. from retries that threw before
        //    updating step status).
        var terminalRunStatuses = new[]
        {
            (int)AutomationRunStatus.Completed,
            (int)AutomationRunStatus.Failed,
            (int)AutomationRunStatus.Cancelled,
            (int)AutomationRunStatus.Rejected,
        };

        var orphanedSteps = await db.StepRuns
            .Where(sr => stuckStepStatuses.Contains(sr.Status)
                && db.AutomationRuns.Any(r => r.Id == sr.RunId && terminalRunStatuses.Contains(r.Status)))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(sr => sr.Status, (int)StepRunStatus.Failed)
                    .SetProperty(sr => sr.CompletedUtc, now)
                    .SetProperty(sr => sr.Error, "Recovered after application restart — parent run already completed"),
                cancellationToken);

        recoveredSteps += orphanedSteps;

        if (recoveredRuns > 0 || recoveredSteps > 0)
        {
            _logger.LogWarning(
                "Recovered {RunCount} stuck automation run(s) and {StepCount} stuck step run(s) from previous process",
                recoveredRuns, recoveredSteps);
        }
    }
}
