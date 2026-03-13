using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// EF Core implementation of <see cref="IAutomationRunRepository"/>.
/// </summary>
internal sealed class EFCoreAutomationRunRepository : IAutomationRunRepository
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;

    public EFCoreAutomationRunRepository(IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public async Task<AutomationRun?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            AutomationRunEntity? runEntity = await db.AutomationRuns
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

            if (runEntity is null)
            {
                return (AutomationRun?)null;
            }

            var stepEntities = await db.StepRuns
                .Where(s => s.RunId == id)
                .OrderBy(s => s.StartedUtc)
                .ToListAsync(cancellationToken);

            var stepRuns = stepEntities.Select(StepRunFactory.BuildDomain).ToList();
            return AutomationRunFactory.BuildDomain(runEntity, stepRuns);
        });

        scope.Complete();
        return result;
    }

    public async Task<(IEnumerable<AutomationRun> Items, int Total)> GetPagedByAutomationAsync(
        Guid automationId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            IQueryable<AutomationRunEntity> query = db.AutomationRuns
                .Where(r => r.AutomationId == automationId);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(r => r.StartedUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);

            return (items, total);
        });

        scope.Complete();
        var runs = result.items.Select(e => AutomationRunFactory.BuildDomain(e));
        return (runs, result.total);
    }

    public async Task<AutomationRun> SaveAsync(AutomationRun run, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var savedRun = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return savedRun;
    }

    public async Task<StepRun> SaveStepRunAsync(StepRun stepRun, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var savedStepRun = await scope.ExecuteWithContextAsync(async db =>
        {
            StepRunEntity? existing = await db.StepRuns.FindAsync([stepRun.Id], cancellationToken);

            if (existing is null)
            {
                StepRunEntity newEntity = StepRunFactory.BuildEntity(stepRun);
                db.StepRuns.Add(newEntity);
            }
            else
            {
                StepRunFactory.UpdateEntity(existing, stepRun);
            }

            await db.SaveChangesAsync(cancellationToken);
            return stepRun;
        });

        scope.Complete();
        return savedStepRun;
    }

    public async Task<int> DeleteByAutomationAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var count = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return count;
    }

    private static readonly int[] TerminalStatuses =
    [
        (int)AutomationRunStatus.Completed,
        (int)AutomationRunStatus.Failed,
        (int)AutomationRunStatus.Cancelled,
    ];

    public async Task<int> DeleteRunsOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var deleted = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return deleted;
    }

    public async Task<int> DeleteExcessRunsAsync(int maxRunsPerAutomation, CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var totalDeleted = await scope.ExecuteWithContextAsync(async db =>
        {
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
        });

        scope.Complete();
        return totalDeleted;
    }

    public async Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
        Guid automationId,
        Guid currentRunId,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            // Get the most recent terminal run before the current one.
            var status = await db.AutomationRuns
                .Where(r => r.AutomationId == automationId
                    && r.Id != currentRunId
                    && TerminalStatuses.Contains(r.Status))
                .OrderByDescending(r => r.StartedUtc)
                .Select(r => (int?)r.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return status.HasValue ? (AutomationRunStatus?)status.Value : null;
        });

        scope.Complete();
        return result;
    }

    public async Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByActionAndStatusAsync(
        string actionAlias,
        StepRunStatus status,
        CancellationToken cancellationToken = default)
    {
        using IEfCoreScope<UmbracoAutomateDbContext> scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var statusInt = (int)status;

            var stepEntities = await db.StepRuns
                .Where(s => s.ActionAlias == actionAlias && s.Status == statusInt)
                .ToListAsync(cancellationToken);

            if (stepEntities.Count == 0)
            {
                return (IReadOnlyList<(AutomationRun, StepRun)>)[];
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

            return (IReadOnlyList<(AutomationRun, StepRun)>)results;
        });

        scope.Complete();
        return result;
    }
}
