using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations;

/// <summary>
/// Publish-time validation of step settings against their settings type, so a step saved with an
/// empty required field (through the Management API or an import) is rejected at publish rather
/// than failing every run.
/// </summary>
public class AutomationServiceStepSettingsValidationTests
{
    private const string LogMessageAlias = "umbracoAutomate.logMessage";
    private const string ForEachAlias = "umbracoAutomate.forEach";
    private const string EndpointActionAlias = "test.endpoint";

    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly AutomationService _service;

    public AutomationServiceStepSettingsValidationTests()
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
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));

        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);
        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        // No configuration keys are set: a $ reference must still count as provided at publish,
        // because the key may only exist on the environment the automation runs on.
        var configReferenceResolver = new ConfigurationReferenceResolver(new ConfigurationBuilder().Build());
        var modelResolver = new EditableModelResolver(configReferenceResolver);

        var actions = new ActionCollection(() =>
        [
            new LogMessageAction(new ActionInfrastructure(modelResolver), NullLogger<LogMessageAction>.Instance),
            new EndpointAction(new ActionInfrastructure(modelResolver)),
        ]);
        var controlFlows = new ControlFlowCollection(() =>
        [
            new ForEachControlFlow(new ControlFlowInfrastructure(modelResolver)),
        ]);
        var triggers = new TriggerCollection(() => []);

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
            configReferenceResolver);
    }

    [Fact]
    public async Task Publish_StepWithEmptyRequiredSetting_IsRejected()
    {
        var automation = GivenDraft(Step(LogMessageAlias, "Log message", "logMessage"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Log message' (logMessage): The Message field is required.");
    }

    [Fact]
    public async Task Publish_StepWithEmptyRequiredSetting_IsNotPublished()
    {
        var automation = GivenDraft(Step(LogMessageAlias, "Log message", "logMessage"));

        await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publish_ControlFlowWithEmptyRequiredSetting_IsRejected()
    {
        var automation = GivenDraft(Step(ForEachAlias, "Each item", "eachItem"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Each item' (eachItem): The Collection field is required.");
    }

    [Fact]
    public async Task Publish_RequiredSettingGivenAsLiteral_Succeeds()
    {
        var automation = GivenDraft(Step(LogMessageAlias, "Log message", "logMessage", ("message", "Hello")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_RequiredSettingGivenAsBinding_Succeeds()
    {
        var automation = GivenDraft(Step(LogMessageAlias, "Log message", "logMessage", ("message", "${ trigger.name }")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_RequiredSettingGivenAsUnsetConfigurationReference_Succeeds()
    {
        var automation = GivenDraft(Step(LogMessageAlias, "Log message", "logMessage", ("message", "$Umbraco:Automate:Variables:Greeting")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_FormatRuleOnBinding_IsLeftToRunTime()
    {
        var automation = GivenDraft(Step(EndpointActionAlias, "Call endpoint", "callEndpoint", ("endpoint", "${ trigger.url }")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_FormatRuleOnConfigurationReference_IsLeftToRunTime()
    {
        var automation = GivenDraft(Step(EndpointActionAlias, "Call endpoint", "callEndpoint", ("endpoint", "$Umbraco:Automate:Variables:Endpoint")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_FormatRuleOnLiteral_IsRejected()
    {
        var automation = GivenDraft(Step(EndpointActionAlias, "Call endpoint", "callEndpoint", ("endpoint", "not a url")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.StartsWith("Step 'Call endpoint' (callEndpoint): ") && e.Contains("Endpoint"));
    }

    [Fact]
    public async Task UpdateDraft_StepWithEmptyRequiredSetting_IsSaved()
    {
        var automation = new AutomationBuilder()
            .AsDraft()
            .WithManualTrigger()
            .AddStep(Step(LogMessageAlias, "Log message", "logMessage"))
            .Build();

        var saved = await _service.UpdateAutomationAsync(automation);

        saved.Status.ShouldBe(AutomationStatus.Draft);
    }

    private Automation GivenDraft(StepConfiguration step)
    {
        var automation = new AutomationBuilder().AsDraft().WithManualTrigger().AddStep(step).Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(automation);
        return automation;
    }

    private static StepConfiguration Step(string actionAlias, string name, string alias, params (string Key, object? Value)[] settings)
    {
        var builder = new StepConfigurationBuilder().WithActionAlias(actionAlias).WithName(name).WithAlias(alias);
        foreach (var (key, value) in settings)
        {
            builder.WithSetting(key, value);
        }

        return builder.Build();
    }

    public sealed class EndpointSettings
    {
        [Field(Label = "Endpoint", SupportsBindings = true)]
        [Url]
        public string Endpoint { get; set; } = string.Empty;
    }

    [Action(EndpointActionAlias, "Endpoint")]
    private sealed class EndpointAction(ActionInfrastructure infrastructure) : ActionBase<EndpointSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
