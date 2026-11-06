// Story AN-5: docs/plans/analytics/STORIES.md
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using static Umbraco.Automate.Tests.Integration.Analytics.AnalyticsDatabase;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// The step-run query of <c>EFCoreAutomateAnalyticsRepository</c> against SQLite: one aggregate per
/// action alias, for step runs that started in the window.
/// </summary>
public class AnalyticsActionQueryTests
{
    private const string HttpRequest = "umbracoAutomate.httpRequest";
    private const string SendEmail = "umbracoAutomate.sendEmail";
    private static readonly Guid Content = Guid.NewGuid();
    private static readonly Guid Commerce = Guid.NewGuid();
    private static readonly DateTime OnSecondOctober = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    public class GivenStepRunsOfTwoActions : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenStepRunsOfTwoActions()
        {
            Automation automation = _db.AddAutomation(Guid.NewGuid());
            AutomationRun run = _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(5));

            _db.AddStepRun(run, HttpRequest, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1), retryCount: 0);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(3), retryCount: 2);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Failed, OnSecondOctober, retryCount: 3, errorCategory: StepRunErrorCategory.Authentication);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Failed, OnSecondOctober, errorCategory: StepRunErrorCategory.Authentication);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Failed, OnSecondOctober, errorCategory: StepRunErrorCategory.Authentication);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Failed, OnSecondOctober, errorCategory: StepRunErrorCategory.Timeout);
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Rejected, OnSecondOctober, TimeSpan.FromSeconds(2));

            for (var i = 0; i < 4; i++) _db.AddStepRun(run, SendEmail, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            for (var i = 0; i < 3; i++) _db.AddStepRun(run, SendEmail, StepRunStatus.Skipped, OnSecondOctober);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task ReturnsOneRowPerAction()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Select(r => r.ActionAlias).ShouldBe([HttpRequest, SendEmail], ignoreOrder: true);
        }

        [Fact]
        public async Task LeavesSkippedStepsOutOfRuns()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single(r => r.ActionAlias == SendEmail).Runs.ShouldBe(4);
        }

        [Fact]
        public async Task CountsCompletedFailedAndRejected()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            var http = rows.Single(r => r.ActionAlias == HttpRequest);
            (http.Completed, http.Failed, http.Rejected).ShouldBe((2, 4, 1));
        }

        [Fact]
        public async Task AddsUpRetries()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single(r => r.ActionAlias == HttpRequest).Retries.ShouldBe(5);
        }

        [Fact]
        public async Task AveragesTheRunTimeOfFinishedSteps()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single(r => r.ActionAlias == SendEmail).AvgDurationTicks.ShouldBe(TimeSpan.FromSeconds(1).Ticks);
        }

        [Fact]
        public async Task CountsFailuresPerErrorCategory()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single(r => r.ActionAlias == HttpRequest).FailuresByCategory.ShouldBe(
                new Dictionary<StepRunErrorCategory, int>
                {
                    [StepRunErrorCategory.Authentication] = 3,
                    [StepRunErrorCategory.Timeout] = 1,
                });
        }
    }

    public class GivenStepRunsInTwoWorkspaces : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenStepRunsInTwoWorkspaces()
        {
            AutomationRun contentRun = _db.AddRun(_db.AddAutomation(Content), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddStepRun(contentRun, HttpRequest, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));

            AutomationRun commerceRun = _db.AddRun(_db.AddAutomation(Commerce), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddStepRun(commerceRun, SendEmail, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddStepRun(commerceRun, SendEmail, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task ReturnsOnlyTheActionsOfTheGivenWorkspace()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Content });

            rows.Select(r => r.ActionAlias).ShouldBe([HttpRequest]);
        }

        [Fact]
        public async Task CountsTheStepRunsOfTheOtherWorkspace()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Commerce });

            rows.Single(r => r.ActionAlias == SendEmail).Runs.ShouldBe(2);
        }
    }

    public class GivenAnAutomationWithStepRunsThatMovedWorkspace : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenAnAutomationWithStepRunsThatMovedWorkspace()
        {
            Automation automation = _db.AddAutomation(Content);
            AutomationRun run = _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.MoveAutomation(automation, Commerce);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsItsStepRunsInItsCurrentWorkspace()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Commerce });

            rows.Single().Runs.ShouldBe(1);
        }
    }

    public class GivenStepsThatNeverFinished : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenStepsThatNeverFinished()
        {
            AutomationRun run = _db.AddRun(_db.AddAutomation(Guid.NewGuid()), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(5));
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Cancelled, OnSecondOctober, TimeSpan.FromHours(1));
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Running, OnSecondOctober, TimeSpan.FromHours(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task AveragesOnlyTheFinishedSteps()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single().AvgDurationTicks.ShouldBe(TimeSpan.FromSeconds(1).Ticks);
        }
    }

    public class GivenAStepThatStartedTheDayAfterItsRun : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenAStepThatStartedTheDayAfterItsRun()
        {
            Automation automation = _db.AddAutomation(Guid.NewGuid());
            AutomationRun run = _db.AddRun(automation, AutomationRunStatus.Completed, new DateTime(2026, 10, 31, 23, 50, 0, DateTimeKind.Utc));
            _db.AddStepRun(run, HttpRequest, StepRunStatus.Completed, new DateTime(2026, 11, 1, 0, 10, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsItOnTheDayTheStepStarted()
        {
            var rows = await _db.Repository.GetActionAggregatesAsync(
                new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc),
                workspaceIds: null);

            rows.Single().Runs.ShouldBe(1);
        }
    }
}
