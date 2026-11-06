using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Runs;

namespace Umbraco.Automate.Persistence.Analytics;

/// <summary>
/// EF Core implementation of <see cref="IAutomateAnalyticsRepository"/>. Every query aggregates in
/// SQL and projects only the aggregate, so no run row is ever materialised.
/// </summary>
internal sealed class EFCoreAutomateAnalyticsRepository : IAutomateAnalyticsRepository
{
    private const int QuarterMinutes = 15;

    private static readonly int Completed = (int)AutomationRunStatus.Completed;
    private static readonly int Rejected = (int)AutomationRunStatus.Rejected;
    private static readonly int Failed = (int)AutomationRunStatus.Failed;
    private static readonly int Suspended = (int)AutomationRunStatus.Suspended;

    private static readonly int[] FinishedStatuses =
    [
        (int)AutomationRunStatus.Completed,
        (int)AutomationRunStatus.Rejected,
        (int)AutomationRunStatus.Cancelled,
        (int)AutomationRunStatus.Failed,
    ];

    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    public EFCoreAutomateAnalyticsRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    public async Task<IReadOnlyList<RunCountGroup>> GetRunCountGroupsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Year/Month/Day/Hour/Minute translate on both SQL Server and SQLite, unlike a DateTime
        // round-down. The quarter start is rebuilt client-side from the group key.
        var groups = await StartedInWindow(db, workspaceIds, fromUtc, toUtc)
            .GroupBy(r => new
            {
                r.StartedUtc!.Value.Year,
                r.StartedUtc.Value.Month,
                r.StartedUtc.Value.Day,
                r.StartedUtc.Value.Hour,
                Quarter = r.StartedUtc.Value.Minute / QuarterMinutes,
                r.Status,
            })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Quarter, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return groups
            .Select(g => new RunCountGroup(
                new DateTime(g.Year, g.Month, g.Day, g.Hour, g.Quarter * QuarterMinutes, 0, DateTimeKind.Utc),
                (AutomationRunStatus)g.Status,
                g.Count))
            .ToList();
    }

    public async Task<long?> GetMedianRunDurationMsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // DurationMs is null for interrupted runs, so they never skew the median.
        var durations = StartedInWindow(db, workspaceIds, fromUtc, toUtc)
            .Where(r => FinishedStatuses.Contains(r.Status) && r.DurationMs != null)
            .Select(r => r.DurationMs!.Value);

        var count = await durations.CountAsync(cancellationToken);
        if (count == 0)
        {
            return null;
        }

        // Fetch only the middle one or two values. For an even count the two middle values are
        // averaged (integer division), which costs the same single small query as taking one.
        var middle = await durations
            .OrderBy(d => d)
            .Skip((count - 1) / 2)
            .Take(count % 2 == 0 ? 2 : 1)
            .ToListAsync(cancellationToken);

        return middle.Count == 0 ? null : middle.Sum() / middle.Count;
    }

    public async Task<int> GetSuspendedRunCountAsync(
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await ScopedRuns(db, workspaceIds)
            .CountAsync(r => r.Status == Suspended, cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationAggregate>> GetAutomationAggregatesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Always joins the automation, even without a workspace scope: the row needs the current
        // name and workspace, and an automation that no longer exists has no row.
        var rows = await (
                from r in db.AutomationRuns
                join a in db.Automations on r.AutomationId equals a.Id
                where r.StartedUtc != null && r.StartedUtc >= fromUtc && r.StartedUtc < toUtc
                    && (workspaceIds == null || workspaceIds.Contains(a.WorkspaceId))
                group r by new { a.Id, a.Name, a.WorkspaceId } into g
                select new
                {
                    AutomationId = g.Key.Id,
                    g.Key.Name,
                    g.Key.WorkspaceId,
                    Runs = g.Count(),
                    Succeeded = g.Count(r => r.Status == Completed || r.Status == Rejected),
                    Failed = g.Count(r => r.Status == Failed),
                    Finished = g.Count(r => FinishedStatuses.Contains(r.Status)),
                    AvgDurationMs = g
                        .Where(r => FinishedStatuses.Contains(r.Status) && r.DurationMs != null)
                        .Average(r => (double?)r.DurationMs),
                    LastStartedUtc = g.Max(r => r.StartedUtc),
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new AutomationAggregate(
                r.AutomationId,
                r.Name,
                r.WorkspaceId,
                r.Runs,
                r.Succeeded,
                r.Failed,
                r.Finished,
                r.AvgDurationMs,
                DateTime.SpecifyKind(r.LastStartedUtc!.Value, DateTimeKind.Utc)))
            .ToList();
    }

    private static IQueryable<AutomationRunEntity> StartedInWindow(
        UmbracoAutomateDbContext db,
        IReadOnlySet<Guid>? workspaceIds,
        DateTime fromUtc,
        DateTime toUtc)
        => ScopedRuns(db, workspaceIds)
            .Where(r => r.StartedUtc != null && r.StartedUtc >= fromUtc && r.StartedUtc < toUtc);

    // Scopes by the automation's current workspace (join), as GetRunCountsByStatusAsync does, so
    // figures agree with the runs list. Null means all workspaces and skips the join.
    private static IQueryable<AutomationRunEntity> ScopedRuns(UmbracoAutomateDbContext db, IReadOnlySet<Guid>? workspaceIds)
    {
        if (workspaceIds is null)
        {
            return db.AutomationRuns;
        }

        return from r in db.AutomationRuns
               join a in db.Automations on r.AutomationId equals a.Id
               where workspaceIds.Contains(a.WorkspaceId)
               select r;
    }
}
