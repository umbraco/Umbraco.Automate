using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// The loop position a ForEach or While step run records (<see cref="StepRun.IterationIndex"/>,
/// <see cref="StepRun.IterationTotal"/>) must survive being saved and re-read, against real SQLite.
/// </summary>
public class StepRunIterationPersistenceTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly EFCoreAutomationRunRepository _runRepository;

    public StepRunIterationPersistenceTests()
    {
        _runRepository = new EFCoreAutomationRunRepository(new TestDbContextFactory(_fixture.CreateContext));
    }

    [Fact]
    public async Task AddStepRunAsync_ForEachIndexAndTotal_ReadsBackBoth()
    {
        StepRun stepRun = await SaveStepRunAsync(iterationIndex: 2, iterationTotal: 5);

        StepRun read = await ReadStepRunAsync(stepRun);
        read.IterationIndex.ShouldBe(2);
        read.IterationTotal.ShouldBe(5);
    }

    [Fact]
    public async Task AddStepRunAsync_WhileTotalOnly_ReadsBackTotalAndNullIndex()
    {
        StepRun stepRun = await SaveStepRunAsync(iterationIndex: null, iterationTotal: 3);

        StepRun read = await ReadStepRunAsync(stepRun);
        read.IterationIndex.ShouldBeNull();
        read.IterationTotal.ShouldBe(3);
    }

    [Fact]
    public async Task AddStepRunAsync_NotALoopStep_ReadsBackNulls()
    {
        StepRun stepRun = await SaveStepRunAsync(iterationIndex: null, iterationTotal: null);

        StepRun read = await ReadStepRunAsync(stepRun);
        read.IterationIndex.ShouldBeNull();
        read.IterationTotal.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateStepRunAsync_IterationSetLater_ReadsBackAfterUpdate()
    {
        StepRun stepRun = await SaveStepRunAsync(iterationIndex: null, iterationTotal: null);

        stepRun.Status = StepRunStatus.Completed;
        stepRun.IterationIndex = 0;
        stepRun.IterationTotal = 1;
        await _runRepository.UpdateStepRunAsync(stepRun);

        StepRun read = await ReadStepRunAsync(stepRun);
        read.IterationIndex.ShouldBe(0);
        read.IterationTotal.ShouldBe(1);
    }

    [Fact]
    public async Task Migrations_AppliedOverExistingStepRunRow_LeaveIterationNull()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"automate_iteration_{Guid.NewGuid():N}.sqlite.db");
        string connectionString = $"Data Source={databasePath};Pooling=false";
        try
        {
            var stepRunId = Guid.NewGuid();
            await using (UmbracoAutomateDbContext before = CreateFileDbContext(connectionString))
            {
                // The database as it was before this change: everything up to AddStepRunBranchOutcome.
                await before.GetService<IMigrator>()
                    .MigrateAsync("UmbracoAutomate_AddStepRunBranchOutcome");

                // SQLite stores Guids as upper-case text, so write them that way for EF to find the row.
                await before.Database.ExecuteSqlRawAsync(
                    "INSERT INTO umbracoAutomateStepRun (Id, RunId, StepId, ActionAlias, Status, RetryCount) " +
                    $"VALUES ('{stepRunId.ToString().ToUpperInvariant()}', '{Guid.NewGuid().ToString().ToUpperInvariant()}', " +
                    $"'{Guid.NewGuid().ToString().ToUpperInvariant()}', 'test.action', 0, 0)");
            }

            await using (UmbracoAutomateDbContext after = CreateFileDbContext(connectionString))
            {
                await after.Database.MigrateAsync();

                StepRunEntity row = await after.StepRuns.SingleAsync(s => s.Id == stepRunId);
                row.IterationIndex.ShouldBeNull();
                row.IterationTotal.ShouldBeNull();
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private async Task<StepRun> SaveStepRunAsync(int? iterationIndex, int? iterationTotal)
    {
        AutomationRun run = new AutomationRunBuilder().Build();
        await _runRepository.SaveAsync(run);

        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            StepId = Guid.NewGuid(),
            ActionAlias = "test.action",
            Status = StepRunStatus.Running,
            IterationIndex = iterationIndex,
            IterationTotal = iterationTotal,
        };
        await _runRepository.AddStepRunAsync(stepRun);
        return stepRun;
    }

    private async Task<StepRun> ReadStepRunAsync(StepRun stepRun)
    {
        AutomationRun? run = await _runRepository.GetAsync(stepRun.RunId);
        return run!.StepRuns.Single(s => s.Id == stepRun.Id);
    }

    private static UmbracoAutomateDbContext CreateFileDbContext(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(
            optionsBuilder, connectionString, Umbraco.Cms.Core.Constants.ProviderNames.SQLLite);
        return new UmbracoAutomateDbContext(optionsBuilder.Options);
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
