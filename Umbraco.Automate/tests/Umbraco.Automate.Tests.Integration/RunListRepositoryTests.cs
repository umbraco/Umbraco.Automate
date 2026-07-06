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
/// Integration tests for <see cref="EFCoreAutomationRunRepository.GetPagedAsync"/> (the cross-automation
/// runs list) against in-memory SQLite. Guards the join-based behaviour that unit tests can't reach:
/// workspace scoping on the automation's <em>current</em> workspace (not the run's snapshot), the
/// server-resolved automation name, ordering, and paging.
/// </summary>
public class RunListRepositoryTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreAutomationRepository _automationRepository;
    private readonly EFCoreAutomationRunRepository _runRepository;

    public RunListRepositoryTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _automationRepository = new EFCoreAutomationRepository(dbContextFactory, CreateFactory());
        _runRepository = new EFCoreAutomationRunRepository(dbContextFactory);
    }

    [Fact]
    public async Task GetPagedAsync_ScopesByCurrentAutomationWorkspace_NotRunSnapshot()
    {
        var currentWorkspace = Guid.NewGuid();
        var oldWorkspace = Guid.NewGuid();

        // The automation now lives in currentWorkspace, but an older run still carries the
        // workspace it executed in (oldWorkspace) — i.e. the automation was moved.
        var automation = await SaveAutomation(currentWorkspace, "Moved automation");
        await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automation.Id)
            .WithWorkspaceId(oldWorkspace)
            .Build());

        // Access to the current workspace -> the run is visible and labelled with the current name.
        var (visible, visibleTotal) = await _runRepository.GetPagedAsync(new HashSet<Guid> { currentWorkspace });
        visibleTotal.ShouldBe(1);
        visible.Single().AutomationName.ShouldBe("Moved automation");

        // Access only to the run's stale snapshot workspace -> not visible.
        var (hidden, hiddenTotal) = await _runRepository.GetPagedAsync(new HashSet<Guid> { oldWorkspace });
        hiddenTotal.ShouldBe(0);
        hidden.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_NullWorkspaceIds_ReturnsRunsFromAllWorkspaces()
    {
        var automationA = await SaveAutomation(Guid.NewGuid(), "A");
        var automationB = await SaveAutomation(Guid.NewGuid(), "B");
        await _runRepository.SaveAsync(new AutomationRunBuilder().WithAutomationId(automationA.Id).Build());
        await _runRepository.SaveAsync(new AutomationRunBuilder().WithAutomationId(automationB.Id).Build());

        var (items, total) = await _runRepository.GetPagedAsync(workspaceIds: null);

        total.ShouldBe(2);
        items.Select(i => i.AutomationName).ShouldBe(["A", "B"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetPagedAsync_OrdersByStartedUtcDescending()
    {
        var automation = await SaveAutomation(Guid.NewGuid(), "Ordered");
        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automation.Id).WithStartedUtc(older).WithInitiatedBy("older").Build());
        await _runRepository.SaveAsync(new AutomationRunBuilder()
            .WithAutomationId(automation.Id).WithStartedUtc(newer).WithInitiatedBy("newer").Build());

        var (items, _) = await _runRepository.GetPagedAsync(workspaceIds: null);

        items.Select(i => i.InitiatedBy).ShouldBe(["newer", "older"]);
    }

    [Fact]
    public async Task GetPagedAsync_HonorsPaging_WithTotalAcrossAllMatches()
    {
        var automation = await SaveAutomation(Guid.NewGuid(), "Paged");
        for (var i = 0; i < 3; i++)
        {
            await _runRepository.SaveAsync(new AutomationRunBuilder()
                .WithAutomationId(automation.Id)
                .WithStartedUtc(new DateTime(2026, 1, 1 + i, 0, 0, 0, DateTimeKind.Utc))
                .Build());
        }

        var (items, total) = await _runRepository.GetPagedAsync(workspaceIds: null, skip: 1, take: 1);

        total.ShouldBe(3);
        items.Count.ShouldBe(1);
    }

    private async Task<Core.Automations.Automation> SaveAutomation(Guid workspaceId, string name)
    {
        var automation = new AutomationBuilder().WithWorkspaceId(workspaceId).WithName(name).Build();
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
