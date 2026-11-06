// Stories AN-2, AN-3, AN-4, AN-6: docs/plans/analytics/STORIES.md
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using static Umbraco.Automate.Tests.Integration.Analytics.AnalyticsDatabase;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// The run queries of <c>EFCoreAutomateAnalyticsRepository</c> against SQLite: counts grouped by
/// UTC quarter hour and status, the median run time, runs waiting right now, and per-automation
/// figures, all scoped on the automation's current workspace.
/// </summary>
public class AnalyticsRunQueryTests
{
    private static readonly Guid Content = Guid.NewGuid();
    private static readonly Guid Commerce = Guid.NewGuid();
    private static readonly DateTime OnSecondOctober = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);

    public class GivenRunsOfEveryStatusOnOneDay : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenRunsOfEveryStatusOnOneDay()
        {
            Automation automation = _db.AddAutomation(Content);
            for (var i = 0; i < 3; i++) _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(automation, AutomationRunStatus.Rejected, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(automation, AutomationRunStatus.Cancelled, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(automation, AutomationRunStatus.Failed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(automation, AutomationRunStatus.Failed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(automation, AutomationRunStatus.Running, OnSecondOctober);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsEveryRunThatStarted()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            groups.Sum(g => g.Count).ShouldBe(8);
        }

        [Fact]
        public async Task CountsTheFailedRuns()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            groups.Where(g => g.Status == AutomationRunStatus.Failed).Sum(g => g.Count).ShouldBe(2);
        }

        [Fact]
        public async Task GroupsByQuarterHourAndStatus()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            groups.Select(g => (g.QuarterStartUtc, g.Status, g.Count)).ShouldBe(
                [
                    (OnSecondOctober, AutomationRunStatus.Completed, 3),
                    (OnSecondOctober, AutomationRunStatus.Rejected, 1),
                    (OnSecondOctober, AutomationRunStatus.Cancelled, 1),
                    (OnSecondOctober, AutomationRunStatus.Failed, 2),
                    (OnSecondOctober, AutomationRunStatus.Running, 1),
                ],
                ignoreOrder: true);
        }
    }

    public class GivenARunThatNeverStarted : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenARunThatNeverStarted()
        {
            Automation automation = _db.AddAutomation(Content);
            _db.AddRun(automation, AutomationRunStatus.Pending, startedUtc: null);
            _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task LeavesItOut()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            groups.Sum(g => g.Count).ShouldBe(1);
        }
    }

    public class GivenFinishedRunsOfDifferentLengths : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenFinishedRunsOfDifferentLengths()
        {
            Automation automation = _db.AddAutomation(Content);
            foreach (var seconds in new[] { 1, 2, 3, 4, 60 })
            {
                _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(seconds));
            }
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task ReturnsTheMedianRunTime()
        {
            long? median = await _db.Repository.GetMedianRunDurationMsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            median.ShouldBe(3000);
        }
    }

    public class GivenRunsSuspendedLastYear : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenRunsSuspendedLastYear()
        {
            Automation automation = _db.AddAutomation(Content);
            _db.AddRun(automation, AutomationRunStatus.Suspended, new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            _db.AddRun(automation, AutomationRunStatus.Suspended, new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsThemAsWaitingNow()
        {
            int waiting = await _db.Repository.GetSuspendedRunCountAsync(workspaceIds: null);

            waiting.ShouldBe(2);
        }
    }

    public class GivenTwoAutomationsThatRanAndOneThatDidNot : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();
        private readonly Automation _a;
        private readonly Automation _b;

        public GivenTwoAutomationsThatRanAndOneThatDidNot()
        {
            _a = _db.AddAutomation(Commerce, "Sync new orders");
            _b = _db.AddAutomation(Content, "Weekly report");
            _db.AddAutomation(Content, "Never ran");

            for (var i = 0; i < 8; i++) _db.AddRun(_a, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(2));
            _db.AddRun(_a, AutomationRunStatus.Rejected, OnSecondOctober, TimeSpan.FromSeconds(2));
            _db.AddRun(_a, AutomationRunStatus.Failed, new DateTime(2026, 10, 6, 14, 2, 0, DateTimeKind.Utc), TimeSpan.FromSeconds(2));
            _db.AddRun(_b, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task ListsOnlyTheAutomationsThatRan()
        {
            var rows = await _db.Repository.GetAutomationAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Select(r => r.AutomationId).ShouldBe([_a.Id, _b.Id], ignoreOrder: true);
        }

        [Fact]
        public async Task GivesEachAutomationItsFigures()
        {
            var rows = await _db.Repository.GetAutomationAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            var a = rows.Single(r => r.AutomationId == _a.Id);
            (a.Runs, a.Succeeded, a.Failed, a.Finished, a.AvgDurationMs, a.LastStartedUtc).ShouldBe(
                (10, 9, 1, 10, 2000d, new DateTime(2026, 10, 6, 14, 2, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public async Task GivesEachAutomationItsWorkspace()
        {
            var rows = await _db.Repository.GetAutomationAggregatesAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            rows.Single(r => r.AutomationId == _a.Id).WorkspaceId.ShouldBe(Commerce);
        }
    }

    public class GivenAnAutomationThatHasBeenDeleted : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();
        private readonly Automation _deleted;

        public GivenAnAutomationThatHasBeenDeleted()
        {
            _deleted = _db.AddAutomation(Content);
            _db.AddRun(_deleted, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.DeleteAutomation(_deleted);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task LeavesItsRunsOutOfTheAutomationRows()
        {
            var rows = await _db.Repository.GetAutomationAggregatesAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Content });

            rows.ShouldNotContain(r => r.AutomationId == _deleted.Id);
        }
    }

    public class GivenRunsInTwoWorkspaces : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenRunsInTwoWorkspaces()
        {
            _db.AddRun(_db.AddAutomation(Content), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(_db.AddAutomation(Commerce), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.AddRun(_db.AddAutomation(Commerce), AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsOnlyTheGivenWorkspaces()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Content });

            groups.Sum(g => g.Count).ShouldBe(1);
        }

        [Fact]
        public async Task CountsEveryWorkspaceWithNoScope()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, workspaceIds: null);

            groups.Sum(g => g.Count).ShouldBe(3);
        }

        [Fact]
        public async Task CountsOneWorkspaceWhenAskedForIt()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Commerce });

            groups.Sum(g => g.Count).ShouldBe(2);
        }
    }

    public class GivenAnAutomationThatMovedWorkspace : IDisposable
    {
        private readonly AnalyticsDatabase _db = new();

        public GivenAnAutomationThatMovedWorkspace()
        {
            Automation automation = _db.AddAutomation(Content);
            _db.AddRun(automation, AutomationRunStatus.Completed, OnSecondOctober, TimeSpan.FromSeconds(1));
            _db.MoveAutomation(automation, Commerce);
        }

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task CountsItsRunsInItsCurrentWorkspace()
        {
            var groups = await _db.Repository.GetRunCountGroupsAsync(WindowStartUtc, WindowEndUtc, new HashSet<Guid> { Commerce });

            groups.Sum(g => g.Count).ShouldBe(1);
        }
    }
}
