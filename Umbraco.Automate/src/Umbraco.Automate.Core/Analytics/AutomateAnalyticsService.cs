using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// Default implementation of <see cref="IAutomateAnalyticsService"/>.
/// </summary>
internal sealed class AutomateAnalyticsService : IAutomateAnalyticsService
{
    private const int MaxRangeDays = 366;

    private readonly IAutomateAnalyticsRepository _repository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IOptionsMonitor<RunCleanupPolicy> _runCleanup;
    private readonly TimeProvider _timeProvider;

    public AutomateAnalyticsService(
        IAutomateAnalyticsRepository repository,
        IWorkspaceService workspaceService,
        IOptionsMonitor<RunCleanupPolicy> runCleanup,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _workspaceService = workspaceService;
        _runCleanup = runCleanup;
        _timeProvider = timeProvider;
    }

    public async Task<Attempt<AnalyticsSummary?, AnalyticsQueryStatus>> GetSummaryAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        var (zone, status) = Validate(query);
        if (zone is null || status != AnalyticsQueryStatus.Success)
        {
            return Attempt.FailWithStatus<AnalyticsSummary?, AnalyticsQueryStatus>(status, null);
        }

        var retainedFrom = GetRetainedFrom(zone);
        (DateOnly From, DateOnly To)? compare = null;
        if (query.CompareFrom is { } compareFrom && query.CompareTo is { } compareTo
            && (retainedFrom is null || compareFrom >= retainedFrom))
        {
            compare = (compareFrom, compareTo);
        }

        // Each repository call opens its own DbContext, so the two ranges and the count run side by side.
        var currentTask = GetPeriodAsync(query.From, query.To, query.WorkspaceIds, zone, cancellationToken);
        var compareTask = compare is { } range
            ? GetPeriodAsync(range.From, range.To, query.WorkspaceIds, zone, cancellationToken)
            : Task.FromResult<AnalyticsPeriod?>(null);
        var suspendedTask = _repository.GetSuspendedRunCountAsync(query.WorkspaceIds, cancellationToken);

        await Task.WhenAll(currentTask, compareTask, suspendedTask);

        return Attempt.SucceedWithStatus<AnalyticsSummary?, AnalyticsQueryStatus>(
            AnalyticsQueryStatus.Success,
            new AnalyticsSummary
            {
                Current = currentTask.Result!,
                Compare = compareTask.Result,
                WaitingForApproval = suspendedTask.Result,
                RetainedFrom = retainedFrom,
            });
    }

    private static (TimeZoneInfo? Zone, AnalyticsQueryStatus Status) Validate(AnalyticsQuery query)
    {
        if (!TryFindZone(query.TimeZoneId, out var zone))
        {
            return (null, AnalyticsQueryStatus.UnknownTimeZone);
        }

        if (query.From > query.To)
        {
            return (zone, AnalyticsQueryStatus.RangeBackwards);
        }

        var length = DayCount(query.From, query.To);
        if (length > MaxRangeDays)
        {
            return (zone, AnalyticsQueryStatus.RangeTooLong);
        }

        if (query.CompareFrom is null != query.CompareTo is null)
        {
            return (zone, AnalyticsQueryStatus.IncompleteCompare);
        }

        if (query.CompareFrom is { } compareFrom && query.CompareTo is { } compareTo
            && (compareFrom > compareTo || DayCount(compareFrom, compareTo) != length))
        {
            return (zone, AnalyticsQueryStatus.CompareLengthMismatch);
        }

        return (zone, AnalyticsQueryStatus.Success);
    }

    private static bool TryFindZone(string? id, out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static int DayCount(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    private DateOnly? GetRetainedFrom(TimeZoneInfo zone)
    {
        // The cleanup job deletes nothing when it is disabled (RunCleanupBackgroundJob).
        var policy = _runCleanup.CurrentValue;
        var retentionDays = policy.RetentionDays;
        if (!policy.Enabled || retentionDays <= 0)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), zone).DateTime);

        // Cleanup deletes runs older than the instant now - RetentionDays, so the oldest day kept whole is one later.
        return today.AddDays(-retentionDays + 1);
    }

    private async Task<AnalyticsPeriod?> GetPeriodAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlySet<Guid>? workspaceIds,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        // The window is [start of `from`, start of the day after `to`) in the user's zone.
        var aggregates = await _repository.GetRunSummaryAggregatesAsync(
            StartOfDayUtc(from, zone),
            StartOfDayUtc(to.AddDays(1), zone),
            workspaceIds,
            cancellationToken);

        var perDay = new SortedDictionary<DateOnly, int[]>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            perDay[day] = new int[DayBucket.Count];
        }

        foreach (var group in aggregates.Groups)
        {
            var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(group.QuarterStartUtc, DateTimeKind.Utc), zone));

            if (perDay.TryGetValue(localDay, out var buckets))
            {
                buckets[DayBucket.Of(group.Status)] += group.Count;
            }
        }

        var days = perDay
            .Select(d => new AnalyticsDay
            {
                Date = d.Key,
                Completed = d.Value[DayBucket.Completed],
                Rejected = d.Value[DayBucket.Rejected],
                Cancelled = d.Value[DayBucket.Cancelled],
                Failed = d.Value[DayBucket.Failed],
                InProgress = d.Value[DayBucket.InProgress],
            })
            .ToList();

        var completed = days.Sum(d => d.Completed);
        var rejected = days.Sum(d => d.Rejected);
        var cancelled = days.Sum(d => d.Cancelled);
        var failed = days.Sum(d => d.Failed);
        var inProgress = days.Sum(d => d.InProgress);

        // A rejection is a person saying no, not a fault, so it counts as a success (SPEC R4).
        var finished = completed + rejected + cancelled + failed;
        decimal? successRate = finished > 0
            ? Math.Round((decimal)(completed + rejected) / finished, 4)
            : null;

        return new AnalyticsPeriod
        {
            Runs = finished + inProgress,
            Completed = completed,
            Rejected = rejected,
            Cancelled = cancelled,
            Failed = failed,
            InProgress = inProgress,
            SuccessRate = successRate,
            MedianDurationMs = aggregates.MedianDurationMs,
            Days = days,
        };
    }

    private static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Some zones skip midnight when the clocks go forward; the day then starts at the first valid time.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(15);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    private static class DayBucket
    {
        public const int Completed = 0;
        public const int Rejected = 1;
        public const int Cancelled = 2;
        public const int Failed = 3;
        public const int InProgress = 4;
        public const int Count = 5;

        public static int Of(AutomationRunStatus status) => status switch
        {
            AutomationRunStatus.Completed => Completed,
            AutomationRunStatus.Rejected => Rejected,
            AutomationRunStatus.Cancelled => Cancelled,
            AutomationRunStatus.Failed => Failed,
            AutomationRunStatus.Pending or AutomationRunStatus.Running or AutomationRunStatus.Suspended => InProgress,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped run status."),
        };
    }
}
