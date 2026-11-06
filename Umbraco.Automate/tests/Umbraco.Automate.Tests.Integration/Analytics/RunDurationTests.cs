// Story AN-2: docs/plans/analytics/STORIES.md
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence;
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

        [Fact]
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

        [Fact]
        public void StoresNoRunTime()
        {
            using var db = _fixture.CreateContext();

            db.AutomationRuns.AsNoTracking().Single(r => r.Id == _runId).DurationMs.ShouldBeNull();
        }
    }

    public class GivenARunThatFinishesAfterBeingSavedAsRunning : IDisposable
    {
        private readonly EfCoreTestFixture _fixture = new();
        private readonly Guid _runId;

        public GivenARunThatFinishesAfterBeingSavedAsRunning()
        {
            var repository = new EFCoreAutomationRunRepository(new TestDbContextFactory(_fixture.CreateContext));
            AutomationRun run = new AutomationRunBuilder()
                .WithStatus(AutomationRunStatus.Running)
                .WithStartedUtc(Started)
                .WithCompletedUtc(null)
                .Build();
            repository.SaveAsync(run).GetAwaiter().GetResult();

            AutomationRun finished = new AutomationRunBuilder()
                .WithId(run.Id)
                .WithStatus(AutomationRunStatus.Completed)
                .WithStartedUtc(Started)
                .WithCompletedUtc(Started.AddMilliseconds(2500))
                .Build();
            repository.SaveAsync(finished).GetAwaiter().GetResult();
            _runId = run.Id;
        }

        public void Dispose() => _fixture.Dispose();

        [Fact]
        public void StoresItsRunTimeOnTheSecondSave()
        {
            using var db = _fixture.CreateContext();

            db.AutomationRuns.AsNoTracking().Single(r => r.Id == _runId).DurationMs.ShouldBe(2500);
        }
    }

    /// <summary>
    /// AN-2 AC7. Applies the real SQLite migrations up to the one before
    /// <c>AddRunAnalytics</c>, inserts a finished run, then applies <c>AddRunAnalytics</c>.
    /// </summary>
    public class GivenAFinishedRunSavedBeforeTheMigration : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"automate_runduration_{Guid.NewGuid():N}.sqlite.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        [Fact]
        public async Task TheMigrationFillsInItsRunTime()
        {
            // A real file with Pooling=false so the migrator and the raw insert each see the same database.
            var connectionString = $"Data Source={_databasePath};Pooling=false";
            var runId = Guid.NewGuid();

            await using (UmbracoAutomateDbContext db = CreateDbContext(connectionString))
            {
                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddWorkflowNodeHeartbeat");

                // EF stores Guids as upper-case TEXT and DateTime as "yyyy-MM-dd HH:mm:ss.FFFFFFF".
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO umbracoAutomateRun (Id, AutomationId, AutomationVersion, WorkspaceId, ServiceAccountKey, Status, StartedUtc, CompletedUtc, InitiatedBy) " +
                    $"VALUES ('{runId.ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', 1, '{Guid.NewGuid().ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', {(int)AutomationRunStatus.Completed}, '2026-10-01 10:00:00', '2026-10-01 10:00:02.5', 'test')");

                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddRunAnalytics");
            }

            await using UmbracoAutomateDbContext readDb = CreateDbContext(connectionString);
            long? durationMs = await readDb.AutomationRuns.AsNoTracking()
                .Where(r => r.Id == runId)
                .Select(r => r.DurationMs)
                .SingleAsync();

            durationMs.ShouldBe(2500);
        }

        private static UmbracoAutomateDbContext CreateDbContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
            UmbracoAutomateDbContext.ConfigureProvider(
                optionsBuilder, connectionString, Umbraco.Cms.Core.Constants.ProviderNames.SQLLite);

            return new UmbracoAutomateDbContext(optionsBuilder.Options);
        }
    }

    /// <summary>
    /// AN-2 AC7. As above, but the run was recovered after a restart, so the
    /// backfill skips it. Applies the real SQLite migrations, inserts the run, then applies <c>AddRunAnalytics</c>.
    /// </summary>
    public class GivenAnInterruptedRunSavedBeforeTheMigration : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"automate_interrupted_{Guid.NewGuid():N}.sqlite.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        [Fact]
        public async Task TheMigrationLeavesItsRunTimeEmpty()
        {
            // A real file with Pooling=false so the migrator and the raw insert each see the same database.
            var connectionString = $"Data Source={_databasePath};Pooling=false";
            var runId = Guid.NewGuid();

            await using (UmbracoAutomateDbContext db = CreateDbContext(connectionString))
            {
                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddWorkflowNodeHeartbeat");

                // EF stores Guids as upper-case TEXT and DateTime as "yyyy-MM-dd HH:mm:ss.FFFFFFF".
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO umbracoAutomateRun (Id, AutomationId, AutomationVersion, WorkspaceId, ServiceAccountKey, Status, StartedUtc, CompletedUtc, InitiatedBy, Error) " +
                    $"VALUES ('{runId.ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', 1, '{Guid.NewGuid().ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', {(int)AutomationRunStatus.Failed}, '2026-10-01 10:00:00', '2026-10-01 10:00:02.5', 'test', 'Recovered after application restart — workflow was interrupted')");

                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddRunAnalytics");
            }

            await using UmbracoAutomateDbContext readDb = CreateDbContext(connectionString);
            long? durationMs = await readDb.AutomationRuns.AsNoTracking()
                .Where(r => r.Id == runId)
                .Select(r => r.DurationMs)
                .SingleAsync();

            durationMs.ShouldBeNull();
        }

        private static UmbracoAutomateDbContext CreateDbContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
            UmbracoAutomateDbContext.ConfigureProvider(
                optionsBuilder, connectionString, Umbraco.Cms.Core.Constants.ProviderNames.SQLLite);

            return new UmbracoAutomateDbContext(optionsBuilder.Options);
        }
    }

    /// <summary>
    /// As above, but the run failed with no error text, so the backfill
    /// still covers it. Applies the real SQLite migrations, inserts the run, then applies <c>AddRunAnalytics</c>.
    /// </summary>
    public class GivenAFailedRunWithNoErrorSavedBeforeTheMigration : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"automate_noerror_{Guid.NewGuid():N}.sqlite.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        [Fact]
        public async Task TheMigrationBackfillsItsRunTime()
        {
            // A real file with Pooling=false so the migrator and the raw insert each see the same database.
            var connectionString = $"Data Source={_databasePath};Pooling=false";
            var runId = Guid.NewGuid();

            await using (UmbracoAutomateDbContext db = CreateDbContext(connectionString))
            {
                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddWorkflowNodeHeartbeat");

                // EF stores Guids as upper-case TEXT and DateTime as "yyyy-MM-dd HH:mm:ss.FFFFFFF".
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO umbracoAutomateRun (Id, AutomationId, AutomationVersion, WorkspaceId, ServiceAccountKey, Status, StartedUtc, CompletedUtc, InitiatedBy, Error) " +
                    $"VALUES ('{runId.ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', 1, '{Guid.NewGuid().ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', {(int)AutomationRunStatus.Failed}, '2026-10-01 10:00:00', '2026-10-01 10:00:02.5', 'test', NULL)");

                await db.GetService<IMigrator>().MigrateAsync("UmbracoAutomate_AddRunAnalytics");
            }

            await using UmbracoAutomateDbContext readDb = CreateDbContext(connectionString);
            long? durationMs = await readDb.AutomationRuns.AsNoTracking()
                .Where(r => r.Id == runId)
                .Select(r => r.DurationMs)
                .SingleAsync();

            durationMs.ShouldBe(2500);
        }

        private static UmbracoAutomateDbContext CreateDbContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
            UmbracoAutomateDbContext.ConfigureProvider(
                optionsBuilder, connectionString, Umbraco.Cms.Core.Constants.ProviderNames.SQLLite);

            return new UmbracoAutomateDbContext(optionsBuilder.Options);
        }
    }
}
