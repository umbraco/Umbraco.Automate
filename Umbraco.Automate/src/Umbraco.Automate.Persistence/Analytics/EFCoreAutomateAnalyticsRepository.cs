using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Actions;
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

    private static readonly int StepCompleted = (int)StepRunStatus.Completed;
    private static readonly int StepFailed = (int)StepRunStatus.Failed;
    private static readonly int StepRejected = (int)StepRunStatus.Rejected;
    private static readonly int StepSkipped = (int)StepRunStatus.Skipped;

    // A cancelled step never finished (its run was killed while it waited), so it counts as a run
    // but not towards the failure rate or the average duration.
    private static readonly int[] StepFinishedStatuses = [StepCompleted, StepFailed, StepRejected];

    // Each level splits a bucket of the one above into at most 100 narrower ones, from ~17 minutes down
    // to a single millisecond. A bucket of at most _maxSortedRows durations is sorted instead of split.
    private static readonly long[] BucketWidthsMs = [1_000_000, 10_000, 100, 1];

    private const int DefaultMaxSortedRows = 10_000;

    private static readonly int UnknownCategory = (int)StepRunErrorCategory.Unknown;

    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;

    private readonly int _maxSortedRows;

    public EFCoreAutomateAnalyticsRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory)
        : this(dbContextFactory, DefaultMaxSortedRows)
    {
    }

    // The sort limit is a parameter so a spec can force the narrowing levels with a handful of rows.
    internal EFCoreAutomateAnalyticsRepository(IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory, int maxSortedRows)
    {
        _dbContextFactory = dbContextFactory;
        _maxSortedRows = maxSortedRows;
    }

    public async Task<RunSummaryAggregates> GetRunSummaryAggregatesAsync(
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
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.Hour,
                g.Key.Quarter,
                g.Key.Status,
                Count = g.Count(),
                TimedCount = g.Count(r => r.DurationMs >= 0),
            })
            .ToListAsync(cancellationToken);

        // The median's population is the finished runs with a (non-negative) duration, which the groups already
        // counted, so the median needs no count query of its own.
        var timedFinishedCount = groups.Where(g => FinishedStatuses.Contains(g.Status)).Sum(g => g.TimedCount);
        var median = await MedianAsync(db, workspaceIds, fromUtc, toUtc, timedFinishedCount, cancellationToken);

        return new RunSummaryAggregates(
            groups
                .Select(g => new RunCountGroup(
                    new DateTime(g.Year, g.Month, g.Day, g.Hour, g.Quarter * QuarterMinutes, 0, DateTimeKind.Utc),
                    (AutomationRunStatus)g.Status,
                    g.Count))
                .ToList(),
            median);
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
                        .Where(r => FinishedStatuses.Contains(r.Status) && r.DurationMs >= 0)
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

    public async Task<IReadOnlyList<ActionAggregate>> GetActionAggregatesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlySet<Guid>? workspaceIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // One scan, one hash aggregate on the raw columns (alias, status, error category), with no
        // conditional aggregates: they cost more per row than a few more buckets. Everything else is
        // derived client-side from the buckets. A failure with no recorded category counts as Unknown,
        // and the average is sum / count so buckets can be merged.
        var buckets = await StepsStartedInWindow(db, workspaceIds, fromUtc, toUtc)
            .GroupBy(s => new { s.ActionAlias, s.Status, s.ErrorCategory })
            .Select(g => new
            {
                g.Key.ActionAlias,
                g.Key.Status,
                g.Key.ErrorCategory,
                Runs = g.Count(),
                Retries = g.Sum(s => s.RetryCount),
                TimedCount = g.Count(s => s.DurationTicks != null),
                TimedTicks = g.Sum(s => (double?)s.DurationTicks),
            })
            .ToListAsync(cancellationToken);

        return buckets
            .GroupBy(b => b.ActionAlias)
            .Select(g =>
            {
                var finished = g.Where(b => StepFinishedStatuses.Contains(b.Status)).ToList();
                var timedCount = finished.Sum(b => b.TimedCount);
                return new ActionAggregate(
                    g.Key,
                    g.Sum(b => b.Runs),
                    g.Where(b => b.Status == StepCompleted).Sum(b => b.Runs),
                    g.Where(b => b.Status == StepFailed).Sum(b => b.Runs),
                    g.Where(b => b.Status == StepRejected).Sum(b => b.Runs),
                    g.Sum(b => b.Retries),
                    timedCount == 0 ? null : finished.Sum(b => b.TimedTicks ?? 0d) / timedCount,
                    g.Where(b => b.Status == StepFailed)
                        .GroupBy(b => (StepRunErrorCategory)(b.ErrorCategory ?? UnknownCategory))
                        .ToDictionary(c => c.Key, c => c.Sum(b => b.Runs)));
            })
            .ToList();
    }

    private static IQueryable<long> TimedFinishedDurations(
        UmbracoAutomateDbContext db,
        IReadOnlySet<Guid>? workspaceIds,
        DateTime fromUtc,
        DateTime toUtc)
        // DurationMs is null for interrupted runs, so they never skew the median. A negative one (clock
        // skew) is not a run time either, and the bucket arithmetic below needs non-negative values.
        => StartedInWindow(db, workspaceIds, fromUtc, toUtc)
            .Where(r => FinishedStatuses.Contains(r.Status) && r.DurationMs >= 0)
            .Select(r => r.DurationMs!.Value);

    private async Task<long?> MedianAsync(
        UmbracoAutomateDbContext db,
        IReadOnlySet<Guid>? workspaceIds,
        DateTime fromUtc,
        DateTime toUtc,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return null;
        }

        // The middle one value, or for an even count the two middle values, averaged with integer
        // division. Ranks are zero-based positions in the sorted durations.
        int[] ranks = count % 2 == 0 ? [(count - 1) / 2, count / 2] : [count / 2];

        var middle = await ValuesAtRanksAsync(
            TimedFinishedDurations(db, workspaceIds, fromUtc, toUtc),
            0,
            long.MaxValue,
            level: 0,
            ranks,
            cancellationToken);

        return middle.Count == ranks.Length ? middle.Sum() / middle.Count : null;
    }

    // Finds the values at the given ranks (ascending, relative to the durations in [lowMs, highMs))
    // without sorting the window. One grouped count per level puts every duration in a bucket of
    // BucketWidthsMs[level] milliseconds. The buckets are walked client-side to the ones holding the
    // ranks. A bucket small enough to sort is fetched with OFFSET/FETCH; a bigger one is split again
    // with the next, narrower width. Width 1 buckets hold one value, so they need no fetch.
    private async Task<List<long>> ValuesAtRanksAsync(
        IQueryable<long> durations,
        long lowMs,
        long highMs,
        int level,
        IReadOnlyList<int> ranks,
        CancellationToken cancellationToken)
    {
        var width = BucketWidthsMs[level];

        var buckets = await durations
            .Where(d => d >= lowMs && d < highMs)
            .GroupBy(d => d / width)
            .Select(g => new { Bucket = g.Key, Count = g.Count() })
            .OrderBy(b => b.Bucket)
            .ToListAsync(cancellationToken);

        var values = new List<long>(ranks.Count);
        var nextRank = 0;
        var before = 0L;

        foreach (var bucket in buckets)
        {
            var after = before + bucket.Count;
            var offsets = new List<int>();
            while (nextRank < ranks.Count && ranks[nextRank] < after)
            {
                offsets.Add((int)(ranks[nextRank] - before));
                nextRank++;
            }

            before = after;
            if (offsets.Count > 0)
            {
                var bucketLowMs = bucket.Bucket * width;
                var bucketHighMs = bucketLowMs + width;

                if (width == 1)
                {
                    values.AddRange(offsets.Select(_ => bucketLowMs));
                }
                else if (bucket.Count <= _maxSortedRows)
                {
                    var first = offsets[0];
                    var sorted = await durations
                        .Where(d => d >= bucketLowMs && d < bucketHighMs)
                        .OrderBy(d => d)
                        .Skip(first)
                        .Take(offsets[^1] - first + 1)
                        .ToListAsync(cancellationToken);
                    values.AddRange(offsets.Where(o => o - first < sorted.Count).Select(o => sorted[o - first]));
                }
                else
                {
                    values.AddRange(await ValuesAtRanksAsync(durations, bucketLowMs, bucketHighMs, level + 1, offsets, cancellationToken));
                }
            }

            if (nextRank == ranks.Count)
            {
                break;
            }
        }

        return values;
    }

    private static IQueryable<AutomationRunEntity> StartedInWindow(
        UmbracoAutomateDbContext db,
        IReadOnlySet<Guid>? workspaceIds,
        DateTime fromUtc,
        DateTime toUtc)
        => ScopedRuns(db, workspaceIds)
            .Where(r => r.StartedUtc != null && r.StartedUtc >= fromUtc && r.StartedUtc < toUtc);

    // Scopes step runs like ScopedRuns, through step run -> run -> automation. Null skips both joins.
    private static IQueryable<StepRunEntity> StepsStartedInWindow(
        UmbracoAutomateDbContext db,
        IReadOnlySet<Guid>? workspaceIds,
        DateTime fromUtc,
        DateTime toUtc)
    {
        IQueryable<StepRunEntity> steps = db.StepRuns
            .Where(s => s.StartedUtc != null && s.StartedUtc >= fromUtc && s.StartedUtc < toUtc && s.Status != StepSkipped);

        if (workspaceIds is null)
        {
            return steps;
        }

        // The workspace's automation ids are a tiny set, so the run ids come from them, and the step
        // runs are matched against those run ids rather than looking up a run per step run.
        var workspaceAutomationIds = db.Automations.Where(a => workspaceIds.Contains(a.WorkspaceId)).Select(a => a.Id);
        var workspaceRunIds = db.AutomationRuns.Where(r => workspaceAutomationIds.Contains(r.AutomationId)).Select(r => r.Id);
        return steps.Where(s => workspaceRunIds.Contains(s.RunId));
    }

    // Scopes by the automation's current workspace (join) so figures agree with the runs list. Null
    // means all workspaces and skips the join, which is safe because
    // AutomationService.DeleteAutomationAsync deletes a run's rows before the automation, so no
    // run is ever orphaned.
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
