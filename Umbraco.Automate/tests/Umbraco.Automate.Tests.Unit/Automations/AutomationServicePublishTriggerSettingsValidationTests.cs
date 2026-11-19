using System.Data;
using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations;

/// <summary>
/// Publish-time validation of trigger settings, for triggers that opt in through
/// <see cref="Core.StepTypes.IPublishValidatableStepType"/> (the MCP trigger), so a setting the
/// endpoint can't serve is caught at publish rather than on every call.
/// </summary>
public class AutomationServicePublishTriggerSettingsValidationTests
{
    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly AutomationService _service;

    public AutomationServicePublishTriggerSettingsValidationTests()
    {
        var scope = new Mock<ICoreScope>();
        scope.Setup(s => s.Notifications).Returns(Mock.Of<IScopedNotificationPublisher>());
        var scopeProvider = new Mock<ICoreScopeProvider>();
        scopeProvider.Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(scope.Object);

        var workspaceService = new Mock<IWorkspaceService>();
        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new WorkspaceBuilder().WithId(id).Build());

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content" }));

        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var configReferenceResolver = new ConfigurationReferenceResolver(new ConfigurationBuilder().Build());
        var modelResolver = new EditableModelResolver(configReferenceResolver);

        var actions = new ActionCollection(() => []);
        var controlFlows = new ControlFlowCollection(() => []);
        var triggers = new TriggerCollection(() =>
        [
            new McpTrigger(new TriggerInfrastructure(modelResolver)),
            new ManualTrigger(new TriggerInfrastructure(modelResolver)),
        ]);

        _service = new AutomationService(
            _repo.Object,
            Mock.Of<IAutomationRunRepository>(),
            Mock.Of<IEntityVersionService>(),
            workspaceService.Object,
            Mock.Of<IConnectionService>(),
            serviceAccountResolver.Object,
            scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            actions,
            triggers,
            controlFlows,
            new SensitiveSettingsStripper(
                actions, triggers, controlFlows, new ConnectionTypeCollection(() => []),
                new WebhookAuthenticatorCollection(() => []),
                new NotificationChannelCollection(() => [])),
            new SectionAccessChecker(),
            configReferenceResolver,
            new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>)));
    }

    [Fact]
    public async Task Publish_McpTriggerWithValidSettings_IsPublished()
    {
        var automation = GivenDraftMcp(new Dictionary<string, object?>
        {
            ["toolName"] = "find_orders",
            ["toolDescription"] = "Finds orders.",
            ["inputFields"] = new List<Dictionary<string, object?>> { new() { ["name"] = "email" } },
        });

        await _service.PublishAutomationAsync(automation.Id);

        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_McpTriggerWithNoSettings_IsRejected()
    {
        var automation = GivenDraftMcp([]);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(error => error.StartsWith("Trigger 'MCP Tool'") && error.Contains("Tool Name"));
    }

    [Fact]
    public async Task Publish_McpTriggerWithInvalidToolName_IsRejected()
    {
        var automation = GivenDraftMcp(new Dictionary<string, object?> { ["toolName"] = "Search products" });

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Trigger 'MCP Tool': Tool name may only contain letters, numbers, hyphens, and underscores.");
    }

    [Fact]
    public async Task Publish_McpTriggerWithOutOfRangeTimeout_IsRejected()
    {
        var automation = GivenDraftMcp(new Dictionary<string, object?> { ["toolName"] = "find_orders", ["toolDescription"] = "Finds orders.", ["timeoutSeconds"] = 0 });

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Trigger 'MCP Tool': Timeout must be between 1 and 300 seconds.");
    }

    [Fact]
    public async Task Publish_McpTriggerWithDuplicateFieldNames_IsRejected()
    {
        var automation = GivenDraftMcp(new Dictionary<string, object?>
        {
            ["toolName"] = "find_orders",
            ["toolDescription"] = "Finds orders.",
            ["inputFields"] = new List<Dictionary<string, object?>> { new() { ["name"] = "email" }, new() { ["name"] = "email" } },
        });

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(error => error.StartsWith("Trigger 'MCP Tool'") && error.Contains("email"));
    }

    [Fact]
    public async Task Publish_TriggerThatDoesNotOptIn_IsNotChecked()
    {
        var automation = new AutomationBuilder().AsDraft().WithManualTrigger().Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(automation);

        await _service.PublishAutomationAsync(automation.Id);

        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private Automation GivenDraftMcp(Dictionary<string, object?> settings)
    {
        var automation = new AutomationBuilder().AsDraft().WithTrigger(McpTrigger.WellKnownAlias, settings).Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(automation);
        return automation;
    }
}
