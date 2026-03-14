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
/// Only runs on <see cref="ServerRole.Single"/> or <see cref="ServerRole.SchedulingPublisher"/>
/// nodes — subscribers must not mark runs as failed that may still be executing elsewhere.
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
        if (_serverRoleAccessor.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher))
        {
            _logger.LogDebug(
                "Stuck run recovery skipped — this node ({ServerRole}) is not the scheduling publisher",
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

        var recovered = await db.AutomationRuns
            .Where(r => NonTerminalStatuses.Contains(r.Status) && !durableRunIds.Contains(r.Id))
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(r => r.Status, (int)AutomationRunStatus.Failed)
                    .SetProperty(r => r.CompletedUtc, now)
                    .SetProperty(r => r.Error, "Recovered after application restart — workflow was interrupted"),
                cancellationToken);

        if (recovered > 0)
        {
            _logger.LogWarning(
                "Recovered {Count} stuck automation run(s) from previous process",
                recovered);
        }
    }
}
