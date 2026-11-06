// Stories AN-4, AN-5: docs/plans/analytics/STORIES.md
// Compiled out until T6 adds the row methods. The T6 builder removes the #if/#endif lines.
#if AUTOMATE_ANALYTICS
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
                .Setup(w => w.GetWorkspaceAsync(Commerce, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Workspace { Id = Commerce, Name = "Commerce", Alias = "commerce" });

            _row = harness.Build().GetAutomationsAsync(Query()).GetAwaiter().GetResult().Result!.Items.Single();
        }

        [Fact(Skip = "Pending")]
        public void CountsItsRuns() => _row.Runs.ShouldBe(10);

        [Fact(Skip = "Pending")]
        public void CountsItsFailures() => _row.Failed.ShouldBe(1);

        [Fact(Skip = "Pending")]
        public void WorksOutItsSuccessRate() => _row.SuccessRate.ShouldBe(0.9m);

        [Fact(Skip = "Pending")]
        public void GivesItsAverageRunTime() => _row.AvgDurationMs.ShouldBe(2000);

        [Fact(Skip = "Pending")]
        public void GivesItsLastRun() => _row.LastRunUtc.ShouldBe(LastRun);

        [Fact(Skip = "Pending")]
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

        [Fact(Skip = "Pending")]
        public void WorksOutItsFailureRate() => _row.FailureRate.ShouldBe(0.2m);

        [Fact(Skip = "Pending")]
        public void PassesItsRetriesThrough() => _row.Retries.ShouldBe(5);

        [Fact(Skip = "Pending")]
        public void GivesItsAverageRunTimeInMilliseconds() => _row.AvgDurationMs.ShouldBe(2000);

        [Fact(Skip = "Pending")]
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

        [Fact(Skip = "Pending")]
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

        [Fact(Skip = "Pending")]
        public void HasNoMostCommonError() => _row.TopErrorCategory.ShouldBeNull();
    }
}
#endif
