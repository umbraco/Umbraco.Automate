// Stories AN-2 to AN-7: docs/plans/analytics/STORIES.md
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Tests.Unit.Analytics;

/// <summary>
/// Builds an <see cref="AutomateAnalyticsService"/> over a mocked repository, with the clock fixed
/// at 12:00 UTC on 6 November 2026 and run retention set per scenario.
/// </summary>
internal sealed class AnalyticsServiceHarness
{
    public static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    public Mock<IAutomateAnalyticsRepository> Repository { get; } = new();

    public Mock<IWorkspaceService> Workspaces { get; } = new();

    public bool Enabled { get; set; } = true;

    public int RetentionDays { get; set; } = 90;

    public List<RunCountGroup> Groups { get; } = [];

    public long? MedianDurationMs { get; set; }

    public AnalyticsServiceHarness()
    {
        Repository
            .Setup(r => r.GetRunSummaryAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<IReadOnlySet<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime from, DateTime to, IReadOnlySet<Guid>? _, CancellationToken _) =>
                new RunSummaryAggregates(Groups.Where(g => g.QuarterStartUtc >= from && g.QuarterStartUtc < to).ToList(), MedianDurationMs));
    }

    public void AddRuns(DateTime startedUtc, AutomationRunStatus status, int count = 1)
    {
        var quarter = new DateTime(startedUtc.Year, startedUtc.Month, startedUtc.Day, startedUtc.Hour, startedUtc.Minute / 15 * 15, 0, DateTimeKind.Utc);
        Groups.Add(new RunCountGroup(quarter, status, count));
    }

    public AutomateAnalyticsService Build()
    {
        var policy = Mock.Of<IOptionsMonitor<RunCleanupPolicy>>(o => o.CurrentValue == new RunCleanupPolicy { Enabled = Enabled, RetentionDays = RetentionDays });
        return new AutomateAnalyticsService(Repository.Object, Workspaces.Object, policy, new FixedTimeProvider(Now));
    }

    public static AnalyticsQuery Query(
        string from = "2026-10-01",
        string to = "2026-10-31",
        string? compareFrom = null,
        string? compareTo = null,
        string timeZone = "UTC")
        => new()
        {
            From = DateOnly.Parse(from),
            To = DateOnly.Parse(to),
            CompareFrom = compareFrom is null ? null : DateOnly.Parse(compareFrom),
            CompareTo = compareTo is null ? null : DateOnly.Parse(compareTo),
            TimeZoneId = timeZone,
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
