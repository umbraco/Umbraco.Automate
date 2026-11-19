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
/// S10: the exit a step took (<see cref="StepRun.BranchOutcome"/>) must survive being saved and
/// re-read, on both the insert and the update path, against real SQLite.
/// </summary>
public class StepRunBranchOutcomePersistenceTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();
    private readonly EFCoreAutomationRunRepository _runRepository;

    public StepRunBranchOutcomePersistenceTests()
    {
        _runRepository = new EFCoreAutomationRunRepository(new TestDbContextFactory(_fixture.CreateContext));
    }

    [Fact]
    public async Task AddStepRunAsync_BranchOutcomeYes_ReadsBackYes()
    {
        StepRun stepRun = await SaveStepRunAsync("yes");

        (await ReadStepRunAsync(stepRun)).BranchOutcome.ShouldBe("yes");
    }

    [Fact]
    public async Task AddStepRunAsync_SwitchStyleBranchOutcomeNews_ReadsBackNews()
    {
        StepRun stepRun = await SaveStepRunAsync("news");

        (await ReadStepRunAsync(stepRun)).BranchOutcome.ShouldBe("news");
    }

    [Fact]
    public async Task AddStepRunAsync_NoBranchOutcome_ReadsBackNull()
    {
        StepRun stepRun = await SaveStepRunAsync(null);

        (await ReadStepRunAsync(stepRun)).BranchOutcome.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateStepRunAsync_BranchOutcomeSetOnCompletion_ReadsBackAfterUpdate()
    {
        StepRun stepRun = await SaveStepRunAsync(null);

        stepRun.Status = StepRunStatus.Completed;
        stepRun.BranchOutcome = "notFound";
        await _runRepository.UpdateStepRunAsync(stepRun);

        (await ReadStepRunAsync(stepRun)).BranchOutcome.ShouldBe("notFound");
    }

    [Fact]
    public async Task AddStepRunAsync_LongBranchOutcome_ReadsBackUnchanged()
    {
        string tooLong = new('x', 300);
        StepRun stepRun = await SaveStepRunAsync(tooLong);

        (await ReadStepRunAsync(stepRun)).BranchOutcome.ShouldBe(tooLong);
    }

    [Fact]
    public async Task Migrations_AppliedOverExistingStepRunRow_LeaveBranchOutcomeNull()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"automate_branchoutcome_{Guid.NewGuid():N}.sqlite.db");
        string connectionString = $"Data Source={databasePath};Pooling=false";
        try
        {
            var stepRunId = Guid.NewGuid();
            await using (UmbracoAutomateDbContext before = CreateFileDbContext(connectionString))
            {
                // The database as it was before this change: everything up to AddStepRunLogEntries.
                await before.GetService<IMigrator>()
                    .MigrateAsync("UmbracoAutomate_AddStepRunLogEntries");

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
                row.BranchOutcome.ShouldBeNull();
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    private async Task<StepRun> SaveStepRunAsync(string? branchOutcome)
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
            BranchOutcome = branchOutcome,
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
