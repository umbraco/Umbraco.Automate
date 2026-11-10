// Stories AN-2, AN-3, AN-7: docs/plans/analytics/STORIES.md
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
            harness.MedianDurationMs = 3000;
            harness.Repository
                .Setup(r => r.GetSuspendedRunCountAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(2);

            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact]
        public void RunsCountsEveryRunThatStarted() => _summary.Current.Runs.ShouldBe(13);

        [Fact]
        public void SuccessRateCountsRejectedAsSuccessAndIgnoresRunsStillGoing() => _summary.Current.SuccessRate.ShouldBe(0.8m);

        [Fact]
        public void CountsTheFailedRuns() => _summary.Current.Failed.ShouldBe(1);

        [Fact]
        public void CountsRunsStillGoingAsInProgress() => _summary.Current.InProgress.ShouldBe(3);

        [Fact]
        public void PassesTheMedianRunTimeThrough() => _summary.Current.MedianDurationMs.ShouldBe(3000);

        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void ReturnsEveryDayOfTheRange() => _summary.Current.Days.Count.ShouldBe(7);

        [Fact]
        public void SplitsEachDayByOutcome()
            => _summary.Current.Days.Single(d => d.Date == new DateOnly(2026, 10, 2)).ShouldBe(
                new AnalyticsDay { Date = new DateOnly(2026, 10, 2), Completed = 3, Rejected = 1, Cancelled = 1, Failed = 2, InProgress = 1 });
    }

    public class GivenARunLateInTheUtcEvening
    {
        private readonly AnalyticsServiceHarness _harness = new();

        public GivenARunLateInTheUtcEvening()
        {
            _harness.AddRuns(new DateTime(2026, 10, 1, 23, 30, 0, DateTimeKind.Utc), AutomationRunStatus.Completed);
            _harness.AddRuns(new DateTime(2026, 10, 1, 18, 20, 0, DateTimeKind.Utc), AutomationRunStatus.Failed);
        }

        [Fact]
        public async Task CountsItOnTheCopenhagenDay()
        {
            var summary = (await _harness.Build().GetSummaryAsync(Query(from: "2026-10-02", to: "2026-10-02", timeZone: "Europe/Copenhagen"))).Result!;

            summary.Current.Completed.ShouldBe(1);
        }

        [Fact]
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

        [Fact]
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

        [Fact]
        public void ReturnsNoComparison() => _summary.Compare.ShouldBeNull();
    }

    public class GivenTheComparisonStartsOnTheOldestDayCleanupDeletes
    {
        [Fact]
        public async Task ReturnsNoComparison()
        {
            // 6 November 2026 minus 90 days: runs from before 9 August are partly deleted.
            var summary = (await new AnalyticsServiceHarness { RetentionDays = 90 }.Build()
                .GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-01", compareFrom: "2026-08-08", compareTo: "2026-08-08"))).Result!;

            summary.Compare.ShouldBeNull();
        }
    }

    public class GivenTheComparisonStartsOnTheOldestDayCleanupKeeps
    {
        [Fact]
        public async Task ReturnsTheComparison()
        {
            var summary = (await new AnalyticsServiceHarness { RetentionDays = 90 }.Build()
                .GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-01", compareFrom: "2026-08-09", compareTo: "2026-08-09"))).Result!;

            summary.Compare.ShouldNotBeNull();
        }
    }

    public class GivenRunCleanupKeepsNinetyDays
    {
        [Fact]
        public async Task RetainedFromIsTheDayAfterTheCutoffDay()
        {
            var summary = (await new AnalyticsServiceHarness { RetentionDays = 90 }.Build()
                .GetSummaryAsync(Query())).Result!;

            summary.RetainedFrom.ShouldBe(new DateOnly(2026, 8, 9));
        }
    }

    public class GivenTheCleanupCutoffFallsOnAnotherLocalDay
    {
        [Fact]
        public async Task RetainedFromIsTheDayAfterTheCutoffsLocalDay()
        {
            // Cutoff 2026-08-08 22:30 UTC is 2026-08-09 00:30 in Copenhagen (CEST), so the first whole day is the 10th.
            var harness = new AnalyticsServiceHarness { RetentionDays = 90, Now = new DateTimeOffset(2026, 11, 6, 22, 30, 0, TimeSpan.Zero) };

            var summary = (await harness.Build().GetSummaryAsync(Query(timeZone: "Europe/Copenhagen"))).Result!;

            summary.RetainedFrom.ShouldBe(new DateOnly(2026, 8, 10));
        }
    }

    public class GivenRunCleanupIsDisabled
    {
        [Fact]
        public async Task RetainedFromIsNull()
        {
            var summary = (await new AnalyticsServiceHarness { Enabled = false, RetentionDays = 90 }.Build()
                .GetSummaryAsync(Query())).Result!;

            summary.RetainedFrom.ShouldBeNull();
        }
    }

    public class GivenRunCleanupIsOff
    {
        private readonly AnalyticsSummary _summary;

        public GivenRunCleanupIsOff()
        {
            var harness = new AnalyticsServiceHarness { RetentionDays = 0 };
            _summary = harness.Build().GetSummaryAsync(Query()).GetAwaiter().GetResult().Result!;
        }

        [Fact]
        public void RetainedFromIsNull() => _summary.RetainedFrom.ShouldBeNull();
    }

    public class GivenABadQuery
    {
        private readonly AutomateAnalyticsService _service = new AnalyticsServiceHarness().Build();

        [Fact]
        public async Task AnUnknownTimeZoneIsRefused()
            => (await _service.GetSummaryAsync(Query(timeZone: "Mars/Olympus"))).Status.ShouldBe(AnalyticsQueryStatus.UnknownTimeZone);

        [Fact]
        public async Task ARefusedQueryNeverReachesTheRepository()
        {
            var harness = new AnalyticsServiceHarness();

            await harness.Build().GetSummaryAsync(Query(timeZone: "Mars/Olympus"));

            harness.Repository.Invocations.ShouldBeEmpty();
        }

        [Fact]
        public async Task ABackwardsRangeIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2026-10-31", to: "2026-10-01"))).Status.ShouldBe(AnalyticsQueryStatus.RangeBackwards);

        [Fact]
        public async Task ARangeOfThreeHundredAndSixtySevenDaysIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2025-10-01", to: "2026-10-02"))).Status.ShouldBe(AnalyticsQueryStatus.RangeTooLong);

        [Fact]
        public async Task HalfAComparisonIsRefused()
            => (await _service.GetSummaryAsync(Query(compareFrom: "2026-09-01"))).Status.ShouldBe(AnalyticsQueryStatus.IncompleteCompare);

        [Fact]
        public async Task AComparisonOfADifferentLengthIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-31", compareFrom: "2026-09-01", compareTo: "2026-09-30"))).Status
                .ShouldBe(AnalyticsQueryStatus.CompareLengthMismatch);

        [Fact]
        public async Task AFromDateBeforeTheYearTwoThousandIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "1999-12-31", to: "2000-01-02"))).Status.ShouldBe(AnalyticsQueryStatus.DateOutOfRange);

        [Fact]
        public async Task ACompareToDateOfTheLastDayOfYearNineThousandNineHundredNinetyNineIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "2026-10-01", to: "2026-10-31", compareFrom: "2026-09-01", compareTo: "9999-12-31"))).Status
                .ShouldBe(AnalyticsQueryStatus.DateOutOfRange);

        [Fact]
        public async Task ARangeEndingOnTheLastDayOfTheCalendarIsRefused()
            => (await _service.GetSummaryAsync(Query(from: "9999-12-30", to: "9999-12-31"))).Status.ShouldBe(AnalyticsQueryStatus.DateOutOfRange);
    }
}
