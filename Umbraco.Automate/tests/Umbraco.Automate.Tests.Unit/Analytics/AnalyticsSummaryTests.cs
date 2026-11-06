// Stories AN-2, AN-3, AN-7: docs/plans/analytics/STORIES.md
// Compiled out until T5 adds the analytics service. The T5 builder removes the #if/#endif lines.
#if AUTOMATE_ANALYTICS
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Runs;
using static Umbraco.Automate.Tests.Unit.Analytics.AnalyticsServiceHarness;

namespace Umbraco.Automate.Tests.Unit.Analytics;

/// <summary>
/// <c>IAutomateAnalyticsService.GetSummaryAsync</c>: totals, rates, the median, waiting approvals,
/// the user's days, the comparison and retention, and the query checks (SPEC R2, R4 to R7, R10).
/// </summary>
public class AnalyticsSummaryTests
{
    private static readonly DateTime OnSecondOctober = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    public class GivenRunsOfMixedStatus
    {
        private readonly AnalyticsSummary _summary;

        public GivenRunsOfMixedStatus()
        {
            var harness = new AnalyticsServiceHarness();
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Completed, 6);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Rejected, 2);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Cancelled, 1);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Failed, 1);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Running, 1);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Suspended, 2);
            harness.Repository
                .Setup(r => r.GetMedianRunDurationMsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3000);
            harness.Repository
                .Setup(r => r.GetSuspendedRunCountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void RunsCountsEveryRunThatStarted() => _summary.Current.Runs.ShouldBe(13);

        [Fact(Skip = "Pending")]
        public void SuccessRateCountsRejectedAsSuccessAndIgnoresRunsStillGoing() => _summary.Current.SuccessRate.ShouldBe(0.8m);

        [Fact(Skip = "Pending")]
        public void CountsTheFailedRuns() => _summary.Current.Failed.ShouldBe(1);

        [Fact(Skip = "Pending")]
        public void CountsRunsStillGoingAsInProgress() => _summary.Current.InProgress.ShouldBe(3);

        [Fact(Skip = "Pending")]
        public void PassesTheMedianRunTimeThrough() => _summary.Current.MedianDurationMs.ShouldBe(3000);

        [Fact(Skip = "Pending")]
        public void ReportsRunsWaitingForApprovalNow() => _summary.WaitingForApproval.ShouldBe(2);
    }

    public class GivenOnlyRunsStillGoing
    {
        private readonly AnalyticsSummary _summary;

        public GivenOnlyRunsStillGoing()
        {
            var harness = new AnalyticsServiceHarness();
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Completed, 4);
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Suspended, 3);
            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void SuccessRateIsWorkedOutOverFinishedRunsOnly() => _summary.Current.SuccessRate.ShouldBe(1m);
    }

    public class GivenNoFinishedRuns
    {
        private readonly AnalyticsSummary _summary;

        public GivenNoFinishedRuns()
        {
            var harness = new AnalyticsServiceHarness();
            harness.AddRuns(OnSecondOctober, AutomationRunStatus.Running, 2);
            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void SuccessRateIsNull() => _summary.Current.SuccessRate.ShouldBeNull();
    }

    public class GivenRunsOnTwoDaysOfAWeek
    {
        private readonly AnalyticsSummary _summary;

        public GivenRunsOnTwoDaysOfAWeek()
        {
            var harness = new AnalyticsServiceHarness();
            harness.AddRuns(new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Completed, 3);
            harness.AddRuns(new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Rejected);
            harness.AddRuns(new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Cancelled);
            harness.AddRuns(new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Failed, 2);
            harness.AddRuns(new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Running);
            harness.AddRuns(new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc), AutomationRunStatus.Completed);
            _summary = harness.Build().GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-07")).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void ReturnsEveryDayOfTheRange() => _summary.Current.Days.Count.ShouldBe(7);

        [Fact(Skip = "Pending")]
        public void SplitsEachDayByOutcome()
            => _summary.Current.Days.Single(d => d.Date == new DateOnly(2026, 10, 2)).ShouldBe(
                new AnalyticsDay(new DateOnly(2026, 10, 2), Completed: 3, Rejected: 1, Cancelled: 1, Failed: 2, InProgress: 1));
    }

    public class GivenARunLateInTheUtcEvening
    {
        private readonly AnalyticsServiceHarness _harness = new();

        public GivenARunLateInTheUtcEvening()
        {
            _harness.AddRuns(new DateTime(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc), AutomationRunStatus.Completed);
            _harness.AddRuns(new DateTime(2026, 10, 1, 18, 20, 0, DateTimeKind.Utc), AutomationRunStatus.Failed);
        }

        [Fact(Skip = "Pending")]
        public async Task CountsItOnTheCopenhagenDay()
        {
            var summary = (await _harness.Build().GetSummaryAsync(Query(from: "2026-10-02", to: "2026-10-02", timeZone: "Europe/Copenhagen"))).Result!;

            summary.Current.Completed.ShouldBe(1);
        }

        [Fact(Skip = "Pending")]
        public async Task CountsAQuarterHourZoneRunOnTheLocalDay()
        {
            var summary = (await _harness.Build().GetSummaryAsync(Query(from: "2026-10-02", to: "2026-10-02", timeZone: "Asia/Kathmandu"))).Result!;

            summary.Current.Failed.ShouldBe(1);
        }
    }

    public class GivenRunsOnTheDayTheClocksGoBack
    {
        private readonly AnalyticsSummary _summary;

        public GivenRunsOnTheDayTheClocksGoBack()
        {
            var harness = new AnalyticsServiceHarness();

            // 00:30 CEST and 23:30 CET on 25 October 2026, Copenhagen time.
            harness.AddRuns(new DateTime(2026, 10, 24, 22, 30, 0, DateTimeKind.Utc), AutomationRunStatus.Completed);
            harness.AddRuns(new DateTime(2026, 10, 25, 22, 30, 0, DateTimeKind.Utc), AutomationRunStatus.Completed);
            _summary = harness.Build().GetSummaryAsync(Query(from: "2026-10-24", to: "2026-10-26", timeZone: "Europe/Copenhagen")).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void CountsBothOnTheTwentyFifth()
            => _summary.Current.Days.Single(d => d.Date == new DateOnly(2026, 10, 25)).Completed.ShouldBe(2);
    }

    public class GivenAComparisonOlderThanRetention
    {
        private readonly AnalyticsSummary _summary;

        public GivenAComparisonOlderThanRetention()
        {
            var harness = new AnalyticsServiceHarness { RetentionDays = 90 };

            // The comparison starts 120 days before 6 November 2026.
            _summary = harness.Build()
                .GetSummaryAsync(Query(from: "2026-08-08", to: "2026-11-05", compareFrom: "2026-05-10", compareTo: "2026-08-07"))
                .GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void ReturnsNoComparison() => _summary.Compare.ShouldBeNull();
    }

    public class GivenRunCleanupIsOff
    {
        private readonly AnalyticsSummary _summary;

        public GivenRunCleanupIsOff()
        {
            var harness = new AnalyticsServiceHarness { RetentionDays = 0 };
            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact(Skip = "Pending")]
        public void RetainedFromIsNull() => _summary.RetainedFrom.ShouldBeNull();
    }

    public class GivenABadQuery
    {
        private readonly AutomateAnalyticsService _service = new AnalyticsServiceHarness().Build();

        [Fact(Skip = "Pending")]
        public async Task AnUnknownTimeZoneIsRefused()
            => (await _service.GetSummaryAsync(Query(timeZone: "Mars/Olympus"))).Status.ShouldBe(AnalyticsQueryStatus.UnknownTimeZone);

        [Fact(Skip = "Pending")]
        public async Task ABackwardsRangeIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2026-10-31", to: "2026-10-01"))).Status.ShouldBe(AnalyticsQueryStatus.RangeBackwards);

        [Fact(Skip = "Pending")]
        public async Task ARangeOfThreeHundredAndSixtySevenDaysIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2025-10-01", to: "2026-10-02"))).Status.ShouldBe(AnalyticsQueryStatus.RangeTooLong);

        [Fact(Skip = "Pending")]
        public async Task HalfAComparisonIsRefused()
            => (await _service.GetSummaryAsync(Query(compareFrom: "2026-09-01"))).Status.ShouldBe(AnalyticsQueryStatus.IncompleteCompare);

        [Fact(Skip = "Pending")]
        public async Task AComparisonOfADifferentLengthIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-31", compareFrom: "2026-09-01", compareTo: "2026-09-30"))).Status
                .ShouldBe(AnalyticsQueryStatus.CompareLengthMismatch);
    }
}
#endif
