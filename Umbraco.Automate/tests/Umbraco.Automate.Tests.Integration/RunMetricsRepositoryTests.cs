using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for the metrics count queries on <see cref="EFCoreAutomationRunRepository"/>
/// against in-memory SQLite. Guards that workspace scoping is applied on the automation's
/// <em>current</em> workspace (via join), consistent with the runs list — not the run's snapshot.
/// </summary>
public class RunMetricsRepositoryTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreAutomationRepository _automationRepository;
    private readonly EFCoreAutomationRunRepository _runRepository;

    public RunMetricsRepositoryTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _automationRepository = new EFCoreAutomationRepository(dbContextFactory, CreateFactory());
        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
    }

    [Fact]
    public async Task GetRunCountsByStatusAsync_ScopesByCurrentAutomationWorkspace_NotRunSnapshot()
    {
        var currentWorkspace = Guid.NewGuid();
        var oldWorkspace = Guid.NewGuid();

        // Automation now in currentWorkspace; a run still snapshots the old workspace it ran in.
        var automation = await SaveAutomation(currentWorkspace);
        await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automation.Id)
            .WithWorkspaceId(oldWorkspace)
            .WithStatus(AutomationRunStatus.Failed)
            .Build());

        var scopedToCurrent = await _runRepository.GetRunCountsByStatusAsync(new HashSet<Guid> { currentWorkspace });
        scopedToCurrent.GetValueOrDefault(AutomationRunStatus.Failed).ShouldBe(1);

        var scopedToOld = await _runRepository.GetRunCountsByStatusAsync(new HashSet<Guid> { oldWorkspace });
        scopedToOld.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRunCountsByStatusAsync_NullWorkspaceIds_CountsAllWorkspaces()
    {
        var a = await SaveAutomation(Guid.NewGuid());
        var b = await SaveAutomation(Guid.NewGuid());
        await _runRepository.SaveAsync(new AutomationRunBuilder().WithAutomationId(a.Id).WithStatus(AutomationRunStatus.Failed).Build());
        await _runRepository.SaveAsync(new AutomationRunBuilder().WithAutomationId(b.Id).WithStatus(AutomationRunStatus.Failed).Build());

        var counts = await _runRepository.GetRunCountsByStatusAsync(workspaceIds: null);

        counts.GetValueOrDefault(AutomationRunStatus.Failed).ShouldBe(2);
    }

    [Fact]
    public async Task GetRunCountsByAutomationAsync_ScopesByCurrentAutomationWorkspace()
    {
        var currentWorkspace = Guid.NewGuid();
        var oldWorkspace = Guid.NewGuid();

        var automation = await SaveAutomation(currentWorkspace);
        await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automation.Id)
            .WithWorkspaceId(oldWorkspace)
            .WithStatus(AutomationRunStatus.Completed)
            .Build());

        var scopedToCurrent = await _runRepository.GetRunCountsByAutomationAsync(new HashSet<Guid> { currentWorkspace });
        scopedToCurrent.Count.ShouldBe(1);
        scopedToCurrent[0].AutomationId.ShouldBe(automation.Id);

        var scopedToOld = await _runRepository.GetRunCountsByAutomationAsync(new HashSet<Guid> { oldWorkspace });
        scopedToOld.ShouldBeEmpty();
    }

    private async Task<Core.Automations.Automation> SaveAutomation(Guid workspaceId)
    {
        var automation = new AutomationBuilder().WithWorkspaceId(workspaceId).Build();
        await _automationRepository.SaveAsync(automation);
        return automation;
    }

    private static AutomationFactory CreateFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
