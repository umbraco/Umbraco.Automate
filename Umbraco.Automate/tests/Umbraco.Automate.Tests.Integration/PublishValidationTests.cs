using System.Data;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for publish-time validation using a mock repository and
/// real workspace validation logic in <see cref="AutomationService"/>.
/// </summary>
public class PublishValidationTests
{
    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly AutomationService _service;

    public PublishValidationTests()
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

        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new WorkspaceBuilder().WithId(id).Build());

        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var actions = new ActionCollection(() => []);
        var triggers = new TriggerCollection(() => []);
        var controlFlows = new ControlFlowCollection(() => []);
        var connectionTypes = new ConnectionTypeCollection(() => []);

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));

        _service = new AutomationService(
            _repo.Object,
            Mock.Of<IAutomationRunRepository>(),
            Mock.Of<IEntityVersionService>(),
            _workspaceService.Object,
            Mock.Of<IConnectionService>(),
            serviceAccountResolver.Object,
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            actions,
            triggers,
            controlFlows,
            new SensitiveSettingsStripper(actions, triggers, controlFlows, connectionTypes),
            new SectionAccessChecker());
    }

    [Fact]
    public async Task Publish_WithValidConfig_Succeeds()
    {
        var automation = SetupAutomation(new AutomationBuilder().AsDraft().WithManualTrigger());

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_WithoutTrigger_Fails()
    {
        var automation = SetupAutomation(new AutomationBuilder().AsDraft());

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.Contains("trigger"));
    }

    [Fact]
    public async Task Publish_WithMissingWorkspace_Fails()
    {
        var automation = SetupAutomation(new AutomationBuilder().AsDraft().WithManualTrigger());

        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.Contains("Workspace"));
    }

    [Fact]
    public async Task Publish_WithDisallowedConnection_Fails()
    {
        var disallowedConnectionId = Guid.NewGuid();

        var step = new StepConfigurationBuilder()
            .WithActionAlias("someAction")
            .WithConnectionId(disallowedConnectionId)
            .Build();

        var automation = SetupAutomation(
            new AutomationBuilder().AsDraft().WithManualTrigger().AddStep(step));

        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder().WithId(automation.WorkspaceId).Build());

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.Contains(disallowedConnectionId.ToString()));
    }

    [Fact]
    public async Task Publish_WithAllowedConnection_Succeeds()
    {
        var connectionId = Guid.NewGuid();

        var step = new StepConfigurationBuilder()
            .WithActionAlias("someAction")
            .WithConnectionId(connectionId)
            .Build();

        var automation = SetupAutomation(
            new AutomationBuilder().AsDraft().WithManualTrigger().AddStep(step));

        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder()
                .WithId(automation.WorkspaceId)
                .WithAllowedConnections(connectionId)
                .Build());

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_WithMultipleErrors_ReturnsAll()
    {
        var disallowedConnectionId = Guid.NewGuid();

        var step = new StepConfigurationBuilder()
            .WithActionAlias("someAction")
            .WithConnectionId(disallowedConnectionId)
            .Build();

        var automation = SetupAutomation(
            new AutomationBuilder().AsDraft().AddStep(step));

        _workspaceService.Setup(w => w.GetWorkspaceAsync(automation.WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder().WithId(automation.WorkspaceId).Build());

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
        ex.Errors.ShouldContain(e => e.Contains("trigger"));
        ex.Errors.ShouldContain(e => e.Contains(disallowedConnectionId.ToString()));
    }

    private Automation SetupAutomation(AutomationBuilder builder)
    {
        var automation = builder.Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        return automation;
    }
}
