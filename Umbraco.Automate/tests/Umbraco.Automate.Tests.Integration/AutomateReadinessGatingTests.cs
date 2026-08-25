using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Reproduces the 2026-07-09 UmbracoCom2024 incident: Umbraco Deploy's disk-triggered import
/// (UmbracoAutomateAutomationServiceConnector.Pass6Async -> AutomationService.CreateAutomationAsync
/// -> EFCoreAutomationRepository.SaveAsync) ran and wrote to umbracoAutomateAutomation before
/// Automate's own startup migration had signalled <see cref="AutomateReadinessSignal"/>, hitting a
/// database whose schema hadn't finished migrating (the migration in flight dropped a NOT NULL
/// column the current code no longer populated).
/// </summary>
public class AutomateReadinessGatingTests : IDisposable
{
    private readonly SqliteConnection _keepAliveConnection;
    private readonly string _connectionString;

    public AutomateReadinessGatingTests()
    {
        var dbName = $"test_{Guid.NewGuid():N}";
        _connectionString = $"DataSource=file:{dbName}?mode=memory&cache=shared";
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        using var context = CreateContext(new AutomateReadinessSignal());
        context.Database.EnsureCreated();
    }

    private UmbracoAutomateDbContext CreateContext(
        AutomateReadinessSignal readinessSignal,
        TimeSpan? waitTimeout = null)
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new AutomateReadinessInterceptor(readinessSignal, waitTimeout))
            .Options;

        return new UmbracoAutomateDbContext(options);
    }

    private static AutomationFactory CreateAutomationFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    /// <summary>
    /// The enlisted path waits with a timeout, because it waits while holding the caller's transaction
    /// — and on SQLite that transaction holds the single per-file write lock, so waiting forever there
    /// stalls every writer on the site instead of one query. Giving up fails the caller's transaction,
    /// which rolls back and releases the lock.
    /// </summary>
    [Fact]
    public async Task SaveAsync_ShouldGiveUp_WhenAnEnlistedWaitExceedsItsTimeout()
    {
        var neverSignalled = new AutomateReadinessSignal();
        var repository = new EFCoreAutomationRepository(
            new TestDbContextFactory(() => CreateContext(neverSignalled, TimeSpan.FromMilliseconds(50))),
            CreateAutomationFactory());

        await Should.ThrowAsync<AutomateNotReadyException>(
            () => repository.SaveAsync(new AutomationBuilder().WithAlias("timed-out").Build()));

        neverSignalled.IsReady.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAsync_ShouldWaitForReadinessSignal_BeforeWritingToDatabase()
    {
        var readinessSignal = new AutomateReadinessSignal();
        var repository = new EFCoreAutomationRepository(
            new TestDbContextFactory(() => CreateContext(readinessSignal)),
            CreateAutomationFactory());

        Task<Umbraco.Automate.Core.Automations.Automation> saveTask = repository.SaveAsync(new AutomationBuilder()
            .WithName("Race")
            .WithAlias("race-alias")
            .Build());

        var completedBeforeReady = await Task.WhenAny(saveTask, Task.Delay(TimeSpan.FromMilliseconds(200))) == saveTask;

        completedBeforeReady.ShouldBeFalse(
            "EFCoreAutomationRepository.SaveAsync wrote to the database before AutomateReadinessSignal " +
            "was signalled. This is the gap that let Umbraco Deploy's disk-triggered import hit a " +
            "not-yet-migrated umbracoAutomateAutomation table during the 2026-07-09 UmbracoCom2024 incident.");

        readinessSignal.Signal();

        await saveTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveAsync_ShouldFailFast_WhenStartupMigrationsFail()
    {
        var readinessSignal = new AutomateReadinessSignal();
        var repository = new EFCoreAutomationRepository(
            new TestDbContextFactory(() => CreateContext(readinessSignal)),
            CreateAutomationFactory());

        Task<Umbraco.Automate.Core.Automations.Automation> saveTask = repository.SaveAsync(new AutomationBuilder()
            .WithName("Migration failure")
            .WithAlias("migration-failure-alias")
            .Build());

        readinessSignal.SignalFailed(new InvalidOperationException("Simulated migration failure"));

        var exception = await Should.ThrowAsync<AutomateNotReadyException>(
            () => saveTask.WaitAsync(TimeSpan.FromSeconds(5)));

        exception.InnerException.ShouldBeOfType<InvalidOperationException>()
            .Message.ShouldBe("Simulated migration failure");
    }

    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
        GC.SuppressFinalize(this);
    }
}
