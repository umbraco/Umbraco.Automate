using System.Data;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class AutomationServiceTests
{
    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly AutomationService _service;

    public AutomationServiceTests()
    {
        _scope.Setup(s => s.Notifications).Returns(_notifications.Object);
        _scopeProvider.Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_scope.Object);

        // Default workspace mock — returns a valid workspace for any ID.
        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder().Build());

        var actions = new ActionCollection(() => []);
        var triggers = new TriggerCollection(() => []);
        var controlFlows = new ControlFlowCollection(() => []);

        _service = new AutomationService(
            _repo.Object,
            _runRepo.Object,
            Mock.Of<IEntityVersionService>(),
            _workspaceService.Object,
            Mock.Of<IConnectionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            actions,
            triggers,
            controlFlows,
            new SensitiveSettingsStripper(actions, triggers, controlFlows));
    }

    [Fact]
    public async Task PublishAutomationAsync_SetsPublishedVersionAndStatus()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(id)
            .AsDraft()
            .WithVersion(3)
            .WithManualTrigger()
            .Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.PublishAutomationAsync(id);

        result.PublishedVersion.ShouldBe(3);
        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task UnpublishAutomationAsync_SetsInactive()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id);
        automation.Status = AutomationStatus.Published;

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.UnpublishAutomationAsync(id);

        result.Status.ShouldBe(AutomationStatus.Inactive);
    }

    [Fact]
    public async Task PublishAutomationAsync_NotFound_Throws()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.PublishAutomationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnpublishAutomationAsync_NotFound_Throws()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UnpublishAutomationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnpublishAutomationAsync_DraftAutomation_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder().WithId(id).AsDraft().Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.UnpublishAutomationAsync(id));

        ex.Errors.ShouldContain(e => e.Contains("Draft"));
        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnpublishAutomationAsync_InactiveAutomation_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder().WithId(id).AsInactive().Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.UnpublishAutomationAsync(id));

        ex.Errors.ShouldContain(e => e.Contains("Inactive"));
        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAutomationAsync_AssignsIdWhenEmpty()
    {
        Automation automation = new AutomationBuilder().AsDraft();

        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.CreateAutomationAsync(automation);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteAutomationAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _service.DeleteAutomationAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAutomationAsync_Found_DeletesRunsAndAutomation()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id);

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteAutomationAsync(id);

        result.ShouldBeTrue();
        _runRepo.Verify(r => r.DeleteByAutomationAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAutomationAsync_NoTrigger_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder().WithId(id).AsDraft().Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(id));

        ex.Errors.ShouldContain(e => e.Contains("trigger"));
    }

    [Fact]
    public async Task PublishAutomationAsync_WorkspaceNotFound_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder().WithId(id).AsDraft().WithManualTrigger().Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(id));

        ex.Errors.ShouldContain(e => e.Contains("Workspace"));
    }

    [Fact]
    public async Task PublishAutomationAsync_DisallowedConnection_ThrowsValidationException()
    {
        var id = Guid.NewGuid();
        var disallowedConnectionId = Guid.NewGuid();

        var step = new StepConfigurationBuilder()
            .WithActionAlias("someAction")
            .WithConnectionId(disallowedConnectionId)
            .Build();

        var automation = new AutomationBuilder()
            .WithId(id)
            .AsDraft()
            .WithManualTrigger()
            .AddStep(step)
            .Build();

        // Workspace allows no connections.
        var workspace = new WorkspaceBuilder().Build();
        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(id));

        ex.Errors.ShouldContain(e => e.Contains(disallowedConnectionId.ToString()));
    }

    [Fact]
    public async Task PublishAutomationAsync_AllowedConnection_Succeeds()
    {
        var id = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var step = new StepConfigurationBuilder()
            .WithActionAlias("someAction")
            .WithConnectionId(connectionId)
            .Build();

        var automation = new AutomationBuilder()
            .WithId(id)
            .AsDraft()
            .WithManualTrigger()
            .AddStep(step)
            .Build();

        // Workspace allows the connection.
        var workspace = new WorkspaceBuilder()
            .WithAllowedConnections(connectionId)
            .Build();
        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.PublishAutomationAsync(id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task PublishAutomationAsync_CancelledByNotification_Throws()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id).AsDraft().WithManualTrigger();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // Simulate notification cancellation
        _notifications.Setup(n => n.PublishCancelableAsync(It.IsAny<AutomationPublishingNotification>()))
            .ReturnsAsync(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => _service.PublishAutomationAsync(id));
    }

    // --- Step alias validation ---

    [Fact]
    public async Task CreateAutomationAsync_DuplicateStepAliases_ThrowsValidationException()
    {
        var automation = new AutomationBuilder()
            .WithAlias("test")
            .WithName("Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.logMessage")
                .WithName("Step 1")
                .WithAlias("myAlias"))
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.logMessage")
                .WithName("Step 2")
                .WithAlias("myAlias"))
            .Build();

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.CreateAutomationAsync(automation));

        ex.Errors.ShouldContain(e => e.Contains("Duplicate"));
    }

    [Fact]
    public async Task CreateAutomationAsync_ReservedStepAlias_ThrowsValidationException()
    {
        var automation = new AutomationBuilder()
            .WithAlias("test")
            .WithName("Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.logMessage")
                .WithName("Step 1")
                .WithAlias("trigger"))
            .Build();

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.CreateAutomationAsync(automation));

        ex.Errors.ShouldContain(e => e.Contains("reserved"));
    }

    [Fact]
    public async Task CreateAutomationAsync_InvalidAliasFormat_ThrowsValidationException()
    {
        var automation = new AutomationBuilder()
            .WithAlias("test")
            .WithName("Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.logMessage")
                .WithName("Step 1")
                .WithAlias("http-request"))
            .Build();

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.CreateAutomationAsync(automation));

        ex.Errors.ShouldContain(e => e.Contains("invalid alias"));
    }

    [Fact]
    public async Task CreateAutomationAsync_NullAliases_AutoGeneratesFromActionAlias()
    {
        Automation? saved = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Automation, Guid?, CancellationToken>((a, _, _) => saved = a)
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var automation = new AutomationBuilder()
            .WithAlias("test")
            .WithName("Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.httpRequest")
                .WithName("Fetch Data"))
            .Build();

        await _service.CreateAutomationAsync(automation);

        saved.ShouldNotBeNull();
        saved.Steps[0].Alias.ShouldBe("httpRequest");
    }

    [Fact]
    public async Task CreateAutomationAsync_DuplicateActionAliases_GeneratesUniqueAliases()
    {
        Automation? saved = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Automation, Guid?, CancellationToken>((a, _, _) => saved = a)
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var automation = new AutomationBuilder()
            .WithAlias("test")
            .WithName("Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.httpRequest")
                .WithName("Fetch 1"))
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.httpRequest")
                .WithName("Fetch 2"))
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.httpRequest")
                .WithName("Fetch 3"))
            .Build();

        await _service.CreateAutomationAsync(automation);

        saved.ShouldNotBeNull();
        saved.Steps[0].Alias.ShouldBe("httpRequest");
        saved.Steps[1].Alias.ShouldBe("httpRequest2");
        saved.Steps[2].Alias.ShouldBe("httpRequest3");
    }

    // --- GenerateUniqueAlias unit tests ---

    [Fact]
    public void GenerateUniqueAlias_ExtractsLastSegment()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alias = AutomationService.GenerateUniqueAlias("umbracoAutomate.httpRequest", used);

        alias.ShouldBe("httpRequest");
    }

    [Fact]
    public void GenerateUniqueAlias_DeduplicatesWithNumber()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "httpRequest" };
        var alias = AutomationService.GenerateUniqueAlias("umbracoAutomate.httpRequest", used);

        alias.ShouldBe("httpRequest2");
    }

    [Fact]
    public void GenerateUniqueAlias_SkipsReservedWords()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var alias = AutomationService.GenerateUniqueAlias("custom.trigger", used);

        // "trigger" is reserved, so it should get a suffix
        alias.ShouldBe("trigger2");
    }
}
