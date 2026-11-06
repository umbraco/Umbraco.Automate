// Story AN-2: docs/plans/analytics/STORIES.md
// Compiled out until T1 adds DurationMs. The T1 builder removes the #if/#endif lines.
#if AUTOMATE_ANALYTICS
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// A finished run stores how long it took in <c>DurationMs</c>, written by the run factory whenever
/// a run is saved with both a start and an end time, and backfilled for older runs by the
/// <c>UmbracoAutomate_AddRunAnalytics</c> migration.
/// </summary>
public class RunDurationTests
{
    private static readonly DateTime Started = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    public class GivenARunThatTookTwoAndAHalfSeconds : IDisposable
    {
        private readonly EfCoreTestFixture _fixture = new();
        private readonly Guid _runId;

        public GivenARunThatTookTwoAndAHalfSeconds()
        {
            var repository = new EFCoreAutomationRunRepository(new TestDbContextFactory(_fixture.CreateContext));
            AutomationRun run = new AutomationRunBuilder()
                .WithStatus(AutomationRunStatus.Completed)
                .WithStartedUtc(Started)
                .WithCompletedUtc(Started.AddMilliseconds(2500))
                .Build();
            repository.SaveAsync(run).GetAwaiter().GetResult();
            _runId = run.Id;
        }

        public void Dispose() => _fixture.Dispose();

        [Fact(Skip = "Pending")]
        public void StoresItsRunTimeInMilliseconds()
        {
            using var db = _fixture.CreateContext();

            db.AutomationRuns.AsNoTracking().Single(r => r.Id == _runId).DurationMs.ShouldBe(2500);
        }
    }

    public class GivenARunThatHasNotEnded : IDisposable
    {
        private readonly EfCoreTestFixture _fixture = new();
        private readonly Guid _runId;

        public GivenARunThatHasNotEnded()
        {
            var repository = new EFCoreAutomationRunRepository(new TestDbContextFactory(_fixture.CreateContext));
            AutomationRun run = new AutomationRunBuilder()
                .WithStatus(AutomationRunStatus.Running)
                .WithStartedUtc(Started)
                .WithCompletedUtc(null)
                .Build();
            repository.SaveAsync(run).GetAwaiter().GetResult();
            _runId = run.Id;
        }

        public void Dispose() => _fixture.Dispose();

        [Fact(Skip = "Pending")]
        public void StoresNoRunTime()
        {
            using var db = _fixture.CreateContext();

            db.AutomationRuns.AsNoTracking().Single(r => r.Id == _runId).DurationMs.ShouldBeNull();
        }
    }

    /// <summary>
    /// AN-2 AC7. The builder applies the real SQLite migrations up to the one before
    /// <c>AddRunAnalytics</c>, inserts a finished run, then applies <c>AddRunAnalytics</c>.
    /// </summary>
    public class GivenAFinishedRunSavedBeforeTheMigration
    {
        [Fact(Skip = "Pending")]
        public void TheMigrationFillsInItsRunTime()
        {
            long? durationMs = null; // Arrange and act per the summary above: read the run's DurationMs after the migration.

            durationMs.ShouldBe(2500);
        }
    }
}
#endif
