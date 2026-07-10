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

    private UmbracoAutomateDbContext CreateContext(AutomateReadinessSignal readinessSignal)
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(new AutomateReadinessInterceptor(readinessSignal))
            .Options;

        return new UmbracoAutomateDbContext(options);
    }

    private static AutomationFactory CreateAutomationFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
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
