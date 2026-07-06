using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Automations;

namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRunRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationRunRepository : IAutomationRunRepository
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreAutomationRunRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<AutomationRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationRunEntity? runEntity = await db.AutomationRuns
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (runEntity is null)
        {
            return null;
        }

        var stepEntities = await db.StepRuns
            .Where(s => s.RunId == id)
            .OrderBy(s => s.StartedUtc)
            .ToListAsync(cancellationToken);

        var stepRuns = stepEntities.Select(StepRunFactory.BuildDomain).ToList();
        return AutomationRunFactory.BuildDomain(runEntity, stepRuns);
    }

    public async Task<AutomationRunStatus?> GetRunStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AutomationRuns
            .Where(r => r.Id == id)
            .Select(r => (AutomationRunStatus?)r.Status)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(IEnumerable<AutomationRun> Items, int Total)> GetPagedByAutomationAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<AutomationRunEntity> query = db.AutomationRuns
            .Where(r => r.AutomationId == automationId);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartedUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var runs = items.Select(e => AutomationRunFactory.BuildDomain(e));
        return (runs, total);
    }

    public async Task<(IReadOnlyList<AutomationRunListItem> Items, int Total)> GetPagedAsync(
        IReadOnlySet<Guid>? workspaceIds,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Join to the automation so results can be scoped by (and labelled with) the
        // automation's current workspace/name rather than the run's execution-time snapshot.
        var query =
            from r in db.AutomationRuns
            join a in db.Automations on r.AutomationId equals a.Id
            where workspaceIds == null || workspaceIds.Contains(a.WorkspaceId)
            select new { Run = r, a.Name };

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Run.StartedUtc)
            .Skip(skip)
            .Take(take)
            .Select(x => new AutomationRunListItem
            {
                Id = x.Run.Id,
                AutomationId = x.Run.AutomationId,
                AutomationName = x.Name,
                AutomationVersion = x.Run.AutomationVersion,
                Status = (AutomationRunStatus)x.Run.Status,
                StartedUtc = x.Run.StartedUtc,
                CompletedUtc = x.Run.CompletedUtc,
                InitiatedBy = x.Run.InitiatedBy,
                CorrelationId = x.Run.CorrelationId,
                Error = x.Run.Error,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<AutomationRun> SaveAsync(AutomationRun run, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        AutomationRunEntity? existing = await db.AutomationRuns.FindAsync([run.Id], cancellationToken);

        if (existing is null)
        {
            AutomationRunEntity newEntity = AutomationRunFactory.BuildEntity(run);
            db.AutomationRuns.Add(newEntity);
        }
        else
        {
            AutomationRunFactory.UpdateEntity(existing, run);
        }

        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task SetWorkflowInstanceIdAsync(
        Guid runId,
        string workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await db.AutomationRuns
            .Where(r => r.Id == runId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.WorkflowInstanceId, workflowInstanceId),
                cancellationToken);
    }

    public async Task<StepRun> AddStepRunAsync(StepRun stepRun, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        db.StepRuns.Add(StepRunFactory.BuildEntity(stepRun));
        await db.SaveChangesAsync(cancellationToken);
        return stepRun;
    }

    public async Task<StepRun> UpdateStepRunAsync(StepRun stepRun, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Attach as Modified and write all columns without a preceding read. The row is expected
        // to exist (it was inserted on the step run's first write, or loaded from the database);
        // if it does not, EF surfaces a DbUpdateConcurrencyException rather than silently no-op.
        db.StepRuns.Update(StepRunFactory.BuildEntity(stepRun));
        await db.SaveChangesAsync(cancellationToken);
        return stepRun;
    }

    public async Task<string?> GetStepRunOutputAsync(Guid stepRunId, Guid runId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.StepRuns
            .Where(s => s.Id == stepRunId && s.RunId == runId)
            .Select(s => s.OutputData)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> DeleteByAutomationAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Delete step runs first (FK constraint)
        var runIds = await db.AutomationRuns
            .Where(r => r.AutomationId == automationId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (runIds.Count == 0)
        {
            return 0;
        }

        await db.StepRuns
            .Where(s => runIds.Contains(s.RunId))
            .ExecuteDeleteAsync(cancellationToken);

        return await db.AutomationRuns
            .Where(r => r.AutomationId == automationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static readonly int[] TerminalStatuses =
    [
        (int)AutomationRunStatus.Completed,
        (int)AutomationRunStatus.Failed,
        (int)AutomationRunStatus.Cancelled,
    ];

    public async Task<int> DeleteRunsOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Only delete terminal runs — never delete running/pending/suspended.
        var runIds = await db.AutomationRuns
            .Where(r => r.StartedUtc < threshold && TerminalStatuses.Contains(r.Status))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (runIds.Count == 0)
        {
            return 0;
        }

        await db.StepRuns
            .Where(s => runIds.Contains(s.RunId))
            .ExecuteDeleteAsync(cancellationToken);

        return await db.AutomationRuns
            .Where(r => runIds.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteExcessRunsAsync(int maxRunsPerAutomation, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var groups = await db.AutomationRuns
            .Where(r => TerminalStatuses.Contains(r.Status))
            .GroupBy(r => r.AutomationId)
            .Where(g => g.Count() > maxRunsPerAutomation)
            .Select(g => new { AutomationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var deleted = 0;
        foreach (var group in groups)
        {
            var runIdsToDelete = await db.AutomationRuns
                .Where(r => r.AutomationId == group.AutomationId && TerminalStatuses.Contains(r.Status))
                .OrderByDescending(r => r.StartedUtc)
                .Skip(maxRunsPerAutomation)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (runIdsToDelete.Count > 0)
            {
                await db.StepRuns
                    .Where(s => runIdsToDelete.Contains(s.RunId))
                    .ExecuteDeleteAsync(cancellationToken);

                deleted += await db.AutomationRuns
                    .Where(r => runIdsToDelete.Contains(r.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        return deleted;
    }

    public async Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
        Guid automationId,
        Guid currentRunId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Get the most recent terminal run before the current one.
        var status = await db.AutomationRuns
            .Where(r => r.AutomationId == automationId
                && r.Id != currentRunId
                && TerminalStatuses.Contains(r.Status))
            .OrderByDescending(r => r.StartedUtc)
            .Select(r => (int?)r.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status.HasValue ? (AutomationRunStatus?)status.Value : null;
    }

    public async Task<IReadOnlyList<AutomationRunStatus>> GetRecentTerminalStatusesAsync(
        Guid automationId,
        int windowSize,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var statuses = await db.AutomationRuns
            .Where(r => r.AutomationId == automationId
                && TerminalStatuses.Contains(r.Status)
                && r.StartedUtc > since)
            .OrderByDescending(r => r.StartedUtc)
            .Take(windowSize)
            .Select(r => r.Status)
            .ToListAsync(cancellationToken);

        return statuses.Select(s => (AutomationRunStatus)s).ToList();
    }

    public async Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByActionAndStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var statusInt = (int)status;

        var stepEntities = await db.StepRuns
            .Where(s => s.ActionAlias == actionAlias && s.Status == statusInt)
            .ToListAsync(cancellationToken);

        if (stepEntities.Count == 0)
        {
            return [];
        }

        var runIds = stepEntities.Select(s => s.RunId).Distinct().ToList();
        var runEntities = await db.AutomationRuns
            .Where(r => runIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        var runLookup = runEntities.ToDictionary(r => r.Id);

        var results = new List<(AutomationRun, StepRun)>();
        foreach (var stepEntity in stepEntities)
        {
            if (runLookup.TryGetValue(stepEntity.RunId, out var runEntity))
            {
                results.Add((AutomationRunFactory.BuildDomain(runEntity), StepRunFactory.BuildDomain(stepEntity)));
            }
        }

        return results;
    }

    public async Task<Dictionary<AutomationRunStatus, int>> GetRunCountsByStatusAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<AutomationRunEntity> query = db.AutomationRuns;

        if (workspaceId.HasValue)
        {
            query = query.Where(r => r.WorkspaceId == workspaceId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(r => r.StartedUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(r => r.StartedUtc <= to.Value);
        }

        var groups = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return groups.ToDictionary(
            g => (AutomationRunStatus)g.Status,
            g => g.Count);
    }

    public async Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
        Guid? workspaceId = null,
        DateTime? from = null,
        DateTime? to = null,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<AutomationRunEntity> query = db.AutomationRuns;

        if (workspaceId.HasValue)
        {
            query = query.Where(r => r.WorkspaceId == workspaceId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(r => r.StartedUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(r => r.StartedUtc <= to.Value);
        }

        var completedStatus = (int)AutomationRunStatus.Completed;
        var failedStatus = (int)AutomationRunStatus.Failed;

        var groups = await query
            .GroupBy(r => r.AutomationId)
            .Select(g => new
            {
                AutomationId = g.Key,
                TotalRuns = g.Count(),
                SuccessCount = g.Count(r => r.Status == completedStatus),
                FailCount = g.Count(r => r.Status == failedStatus),
            })
            .OrderByDescending(g => g.TotalRuns)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Resolve automation names
        var automationIds = groups.Select(g => g.AutomationId).ToList();
        var automationNames = await db.Automations
            .Where(a => automationIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Name })
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);

        return groups.Select(g => new AutomationRunCount
        {
            AutomationId = g.AutomationId,
            AutomationName = automationNames.GetValueOrDefault(g.AutomationId, "Unknown"),
            TotalRuns = g.TotalRuns,
            SuccessCount = g.SuccessCount,
            FailCount = g.FailCount,
        }).ToList();
    }

    public async Task<int> GetRecentRunCountAsync(
        Guid automationId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.AutomationRuns
            .CountAsync(r => r.AutomationId == automationId && r.StartedUtc >= since, cancellationToken);
    }

    public async Task<int> GetConcurrentRunCountAsync(
        Guid automationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var runningStatuses = new[]
        {
            (int)AutomationRunStatus.Running,
            (int)AutomationRunStatus.Pending,
        };

        return await db.AutomationRuns
            .CountAsync(r => r.AutomationId == automationId && runningStatuses.Contains(r.Status), cancellationToken);
    }
}
