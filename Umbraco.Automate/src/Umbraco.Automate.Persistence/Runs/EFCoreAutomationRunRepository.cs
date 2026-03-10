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
}
