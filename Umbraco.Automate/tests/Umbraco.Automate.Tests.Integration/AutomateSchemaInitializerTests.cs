using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Workspaces;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Exercises <see cref="AutomateSchemaInitializer"/> against a real, file-backed SQLite database, so
/// that <c>Database.MigrateAsync</c> and the migrations in
/// <c>Umbraco.Automate.Persistence.Sqlite</c> actually run.
/// </summary>
/// <remarks>
/// The regression this protects is umbraco/Umbraco.Automate#198: Umbraco Deploy's boot-time restore
/// queried <c>umbracoAutomateWorkspace</c> before Automate had created it, and failed with a raw
/// <c>no such table</c> provider error. The initializer is what makes the schema exist first, and
/// <c>AutomateSchemaComponent</c> is what makes it happen early enough.
/// </remarks>
public class AutomateSchemaInitializerTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public AutomateSchemaInitializerTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"automate_schema_{Guid.NewGuid():N}.sqlite.db");

        // A real file, not a shared-cache in-memory database: migrating is the thing under test, and
        // Pooling=false so the file is not still held open when the test deletes it.
        _connectionString = $"Data Source={_databasePath};Pooling=false";
    }

    [Fact]
    public async Task EnsureMigratedAsync_CreatesTheSchemaAndSignalsReady()
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal);

        await initializer.EnsureMigratedAsync();

        readinessSignal.IsReady.ShouldBeTrue();
        readinessSignal.HasFailed.ShouldBeFalse();
        (await GetAppliedMigrationCount()).ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// The exact failure reported in #198, and its fix. Before the schema is initialized the
    /// connector's create-vs-update read throws <c>no such table: umbracoAutomateWorkspace</c>;
    /// afterwards the same read succeeds and reports "not found", which is what lets Deploy create it.
    /// </summary>
    [Fact]
    public async Task WorkspaceRepository_CanOnlyBeRead_OnceTheSchemaIsInitialized()
    {
        var repository = new EFCoreWorkspaceRepository(new TestDbContextFactory(CreateDbContext));

        SqliteException exception = await Should.ThrowAsync<SqliteException>(
            () => repository.GetAsync(Guid.NewGuid()));
        exception.Message.ShouldContain("umbracoAutomateWorkspace");

        using AutomateSchemaInitializer initializer = CreateInitializer(new AutomateReadinessSignal());
        await initializer.EnsureMigratedAsync();

        (await repository.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }

    /// <summary>
    /// Components are initialized again on a runtime restart, and the startup notification handler
    /// calls the initializer as a safety net, so a second call is normal and must be a no-op.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_IsIdempotent()
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal);

        await initializer.EnsureMigratedAsync();
        var appliedAfterFirstCall = await GetAppliedMigrationCount();

        await initializer.EnsureMigratedAsync();

        readinessSignal.IsReady.ShouldBeTrue();
        (await GetAppliedMigrationCount()).ShouldBe(appliedAfterFirstCall);
    }

    /// <summary>
    /// A fresh initializer over an already-migrated database has nothing pending, and must still
    /// report ready rather than leaving waiters blocked. This is the every-boot-after-the-first case.
    /// </summary>
    [Fact]
    public async Task EnsureMigratedAsync_SignalsReady_WhenThereIsNothingPending()
    {
        using (AutomateSchemaInitializer first = CreateInitializer(new AutomateReadinessSignal()))
        {
            await first.EnsureMigratedAsync();
        }

        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer second = CreateInitializer(readinessSignal);

        await second.EnsureMigratedAsync();

        readinessSignal.IsReady.ShouldBeTrue();
    }

    [Fact]
    public async Task EnsureMigratedAsync_MigratesOnce_WhenCalledConcurrently()
    {
        var readinessSignal = new AutomateReadinessSignal();
        using AutomateSchemaInitializer initializer = CreateInitializer(readinessSignal);

        await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => initializer.EnsureMigratedAsync())));

        readinessSignal.IsReady.ShouldBeTrue();
        readinessSignal.HasFailed.ShouldBeFalse();
    }

    private AutomateSchemaInitializer CreateInitializer(AutomateReadinessSignal readinessSignal)
        => new(
            BuildConfiguration(),
            Mock.Of<IOptionsMonitor<ConnectionStrings>>(),
            readinessSignal,
            Mock.Of<IRuntimeState>(x => x.Level == RuntimeLevel.Run),
            NullLogger<AutomateSchemaInitializer>.Instance);

    private IConfiguration BuildConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{DatabaseConnectionInfo.ConnectionStringName}"] = _connectionString,
                [$"ConnectionStrings:{DatabaseConnectionInfo.ConnectionStringName}_ProviderName"] =
                    Umbraco.Cms.Core.Constants.ProviderNames.SQLLite,
            })
            .Build();

    private UmbracoAutomateDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
        UmbracoAutomateDbContext.ConfigureProvider(
            optionsBuilder, _connectionString, Umbraco.Cms.Core.Constants.ProviderNames.SQLLite);

        return new UmbracoAutomateDbContext(optionsBuilder.Options);
    }

    private async Task<int> GetAppliedMigrationCount()
    {
        await using UmbracoAutomateDbContext dbContext = CreateDbContext();

        return (await dbContext.Database.GetAppliedMigrationsAsync()).Count();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        GC.SuppressFinalize(this);
    }
}
