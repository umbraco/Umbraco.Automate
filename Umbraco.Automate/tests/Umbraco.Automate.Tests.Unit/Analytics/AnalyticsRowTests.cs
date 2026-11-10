// Stories AN-4, AN-5: docs/plans/analytics/STORIES.md
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Workspaces;
using static Umbraco.Automate.Tests.Unit.Analytics.AnalyticsServiceHarness;

namespace Umbraco.Automate.Tests.Unit.Analytics;

/// <summary>
/// <c>GetAutomationsAsync</c> and <c>GetActionsAsync</c>: rates, averages, workspace names and the
/// most common error (SPEC R4, R5, R8, R9).
/// </summary>
public class AnalyticsRowTests
{
    private static readonly Guid Commerce = Guid.NewGuid();
    private static readonly Guid AutomationA = Guid.NewGuid();
    private static readonly DateTime LastRun = new(2026, 11, 6, 14, 2, 0, DateTimeKind.Utc);

    public class GivenAnAutomationThatRanTenTimes
    {
        private readonly AutomationAnalyticsRow _row;

        public GivenAnAutomationThatRanTenTimes()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AutomationAggregate(AutomationA, "Sync new orders", Commerce, Runs: 10, Succeeded: 9, Failed: 1, Finished: 10, AvgDurationMs: 2000, LastStartedUtc: LastRun)]);
            harness.Workspaces
                .Setup(w => w.GetAllWorkspacesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Workspace { Id = Commerce, Name = "Commerce", Alias = "commerce" }]);

            _row = harness.Build().GetAutomationsAsync(Query()).GetAwaiter().GetResult().Result!.Items.Single();
        }

        [Fact]
        public void CountsItsRuns() => _row.Runs.ShouldBe(10);

        [Fact]
        public void CountsItsFailures() => _row.Failed.ShouldBe(1);

        [Fact]
        public void WorksOutItsSuccessRate() => _row.SuccessRate.ShouldBe(0.9m);

        [Fact]
        public void GivesItsAverageRunTime() => _row.AvgDurationMs.ShouldBe(2000);

        [Fact]
        public void GivesItsLastRun() => _row.LastRunUtc.ShouldBe(LastRun);

        [Fact]
        public void NamesItsWorkspace() => _row.WorkspaceName.ShouldBe("Commerce");
    }

    public class GivenAnActionWithFailures
    {
        private readonly ActionAnalyticsRow _row;

        public GivenAnActionWithFailures()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new ActionAggregate(
                        "umbracoAutomate.httpRequest",
                        Runs: 10, Completed: 7, Failed: 2, Rejected: 1, Retries: 5,
                        AvgDurationTicks: TimeSpan.FromSeconds(2).Ticks,
                        FailuresByCategory: new Dictionary<StepRunErrorCategory, int>
                        {
                            [StepRunErrorCategory.Authentication] = 3,
                            [StepRunErrorCategory.Timeout] = 1,
                        }),
                ]);

            _row = harness.Build().GetActionsAsync(Query()).GetAwaiter().GetResult().Result!.Items.Single();
        }

        [Fact]
        public void WorksOutItsFailureRate() => _row.FailureRate.ShouldBe(0.2m);

        [Fact]
        public void PassesItsRetriesThrough() => _row.Retries.ShouldBe(5);

        [Fact]
        public void GivesItsAverageRunTimeInMilliseconds() => _row.AvgDurationMs.ShouldBe(2000);

        [Fact]
        public void NamesTheMostCommonError() => _row.TopErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    public class GivenTwoErrorCategoriesTied
    {
        private readonly ActionAnalyticsRow _row;

        public GivenTwoErrorCategoriesTied()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new ActionAggregate(
                        "umbracoAutomate.sendEmail",
                        Runs: 4, Completed: 2, Failed: 2, Rejected: 0, Retries: 0, AvgDurationTicks: null,
                        FailuresByCategory: new Dictionary<StepRunErrorCategory, int>
                        {
                            [StepRunErrorCategory.Timeout] = 1,
                            [StepRunErrorCategory.Authentication] = 1,
                        }),
                ]);

            _row = harness.Build().GetActionsAsync(Query()).GetAwaiter().GetResult().Result!.Items.Single();
        }

        [Fact]
        public void TheLowerEnumValueWins() => _row.TopErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    public class GivenAnActionWithNoFailures
    {
        private readonly ActionAnalyticsRow _row;

        public GivenAnActionWithNoFailures()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new ActionAggregate(
                        "umbracoAutomate.publishContent",
                        Runs: 3, Completed: 3, Failed: 0, Rejected: 0, Retries: 0, AvgDurationTicks: TimeSpan.FromSeconds(1).Ticks,
                        FailuresByCategory: new Dictionary<StepRunErrorCategory, int>()),
                ]);

            _row = harness.Build().GetActionsAsync(Query()).GetAwaiter().GetResult().Result!.Items.Single();
        }

        [Fact]
        public void HasNoMostCommonError() => _row.TopErrorCategory.ShouldBeNull();
    }

    public class GivenAutomationsWithAComparisonInsideRetention
    {
        private readonly AnalyticsRows<AutomationAnalyticsRow> _rows;

        public GivenAutomationsWithAComparisonInsideRetention()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(new DateTime(2026, 10, 1, 0, 0, 0), new DateTime(2026, 11, 1, 0, 0, 0), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AutomationAggregate(AutomationA, "Current", Commerce, Runs: 10, Succeeded: 10, Failed: 0, Finished: 10, AvgDurationMs: null, LastStartedUtc: LastRun)]);
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(new DateTime(2026, 8, 31, 0, 0, 0), new DateTime(2026, 10, 1, 0, 0, 0), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AutomationAggregate(AutomationA, "Previous", Commerce, Runs: 4, Succeeded: 4, Failed: 0, Finished: 4, AvgDurationMs: null, LastStartedUtc: LastRun)]);

            _rows = harness.Build().GetAutomationsAsync(Query(compareFrom: "2026-08-31", compareTo: "2026-09-30")).GetAwaiter().GetResult().Result!;
        }

        [Fact]
        public void TheComparisonRowsComeFromTheComparisonWindow() => _rows.CompareItems!.Single().Name.ShouldBe("Previous");
    }

    public class GivenAutomationsWithAComparisonBeforeRetention
    {
        [Fact]
        public async Task TheComparisonRowsAreNull()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var rows = (await harness.Build()
                .GetAutomationsAsync(Query(compareFrom: "2026-08-08", compareTo: "2026-09-07"))).Result!;

            rows.CompareItems.ShouldBeNull();
        }
    }

    public class GivenActionsWithAComparisonInsideRetention
    {
        private readonly AnalyticsRows<ActionAnalyticsRow> _rows;

        public GivenActionsWithAComparisonInsideRetention()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(new DateTime(2026, 10, 1, 0, 0, 0), new DateTime(2026, 11, 1, 0, 0, 0), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ActionAggregate("current.action", Runs: 10, Completed: 10, Failed: 0, Rejected: 0, Retries: 0, AvgDurationTicks: null, FailuresByCategory: new Dictionary<StepRunErrorCategory, int>())]);
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(new DateTime(2026, 8, 31, 0, 0, 0), new DateTime(2026, 10, 1, 0, 0, 0), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new ActionAggregate("previous.action", Runs: 4, Completed: 4, Failed: 0, Rejected: 0, Retries: 0, AvgDurationTicks: null, FailuresByCategory: new Dictionary<StepRunErrorCategory, int>())]);

            _rows = harness.Build().GetActionsAsync(Query(compareFrom: "2026-08-31", compareTo: "2026-09-30")).GetAwaiter().GetResult().Result!;
        }

        [Fact]
        public void TheComparisonRowsComeFromTheComparisonWindow() => _rows.CompareItems!.Single().ActionAlias.ShouldBe("previous.action");
    }

    public class GivenActionsWithAComparisonBeforeRetention
    {
        [Fact]
        public async Task TheComparisonRowsAreNull()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetActionAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var rows = (await harness.Build()
                .GetActionsAsync(Query(compareFrom: "2026-08-08", compareTo: "2026-09-07"))).Result!;

            rows.CompareItems.ShouldBeNull();
        }
    }

    public class GivenAnAutomationWhoseWorkspaceIsGone
    {
        [Fact]
        public async Task TheWorkspaceNameIsEmpty()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AutomationAggregate(AutomationA, "Orphan", Guid.NewGuid(), Runs: 1, Succeeded: 1, Failed: 0, Finished: 1, AvgDurationMs: null, LastStartedUtc: LastRun)]);
            harness.Workspaces
                .Setup(w => w.GetAllWorkspacesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new Workspace { Id = Commerce, Name = "Commerce", Alias = "commerce" }]);

            var rows = (await harness.Build().GetAutomationsAsync(Query())).Result!;

            rows.Items.Single().WorkspaceName.ShouldBe(string.Empty);
        }
    }

    public class GivenAnAutomationWithNothingFinished
    {
        [Fact]
        public async Task TheSuccessRateIsNull()
        {
            var harness = new AnalyticsServiceHarness();
            harness.Repository
                .Setup(r => r.GetAutomationAggregatesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync([new AutomationAggregate(AutomationA, "Busy", Commerce, Runs: 2, Succeeded: 0, Failed: 0, Finished: 0, AvgDurationMs: null, LastStartedUtc: LastRun)]);
            harness.Workspaces
                .Setup(w => w.GetAllWorkspacesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            var rows = (await harness.Build().GetAutomationsAsync(Query())).Result!;

            rows.Items.Single().SuccessRate.ShouldBeNull();
        }
    }

    public class GivenAnInvalidQueryForAutomations
    {
        [Fact]
        public async Task TheRepositoryIsNeverCalled()
        {
            var harness = new AnalyticsServiceHarness();

            await harness.Build().GetAutomationsAsync(Query(timeZone: "Not/AZone"));

            harness.Repository.Invocations.ShouldBeEmpty();
        }

        [Fact]
        public async Task ARangeEndingOnTheLastDayOfTheCalendarIsRefused()
            => (await new AnalyticsServiceHarness().Build().GetAutomationsAsync(Query(from: "9999-12-30", to: "9999-12-31"))).Status
                .ShouldBe(AnalyticsQueryStatus.DateOutOfRange);
    }

    public class GivenAnInvalidQueryForActions
    {
        [Fact]
        public async Task TheRepositoryIsNeverCalled()
        {
            var harness = new AnalyticsServiceHarness();

            await harness.Build().GetActionsAsync(Query(timeZone: "Not/AZone"));

            harness.Repository.Invocations.ShouldBeEmpty();
        }
    }
}
