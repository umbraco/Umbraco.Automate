using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Conditions;
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
/// Publish-time check that every <c>${ }</c> binding the run will evaluate only uses binding
/// filters that exist, so a mistyped filter name is caught before the automation goes live
/// rather than passing the value through unfiltered or failing every run.
/// </summary>
public class AutomationServicePublishBindingFilterValidationTests
{
    private const string LogMessageAlias = "umbracoAutomate.logMessage";
    private const string IfAlias = "umbracoAutomate.if";
    private const string NotesActionAlias = "test.notes";

    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly AutomationService _service;

    public AutomationServicePublishBindingFilterValidationTests()
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

        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var configReferenceResolver = new ConfigurationReferenceResolver(new ConfigurationBuilder().Build());
        var modelResolver = new EditableModelResolver(configReferenceResolver);

        var actions = new ActionCollection(() =>
        [
            new LogMessageAction(new ActionInfrastructure(modelResolver), NullLogger<LogMessageAction>.Instance),
            new NotesAction(new ActionInfrastructure(modelResolver)),
        ]);
        var controlFlows = new ControlFlowCollection(() =>
        [
            new IfControlFlow(new ControlFlowInfrastructure(modelResolver)),
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
            configReferenceResolver,
            new BindingEvaluator(new BindingFilterCollection(() => [new UppercaseFilter(), new LowercaseFilter()])));
    }

    [Fact]
    public async Task Publish_SettingWithUnknownFilter_IsRejectedNamingTheStepFieldAndFilter()
    {
        var automation = GivenDraft(LogStep(("message", "Hello ${ trigger.name | uppercse }")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Log message' (logMessage): Field 'Message' uses the binding filter 'uppercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_PipeInsideAFilterArgument_IsRejectedWithoutEchoingTheArgument()
    {
        var automation = GivenDraft(LogStep(("message", "${ trigger.name | uppercase | top secret }")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Log message' (logMessage): Field 'Message' uses a binding filter name that is not valid. A '|' inside a filter argument starts a new filter.");
    }

    [Fact]
    public async Task Publish_SettingWithUnknownFilter_IsNotPublished()
    {
        var automation = GivenDraft(LogStep(("message", "${ trigger.name | uppercse }")));

        await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        _repo.Verify(
            r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Publish_SettingWithKnownFilter_Succeeds()
    {
        var automation = GivenDraft(LogStep(("message", "${ trigger.name | uppercase }")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_SettingWithKnownFilterInADifferentCase_Succeeds()
    {
        var automation = GivenDraft(LogStep(("message", "${ trigger.name | UpperCase }")));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_FieldThatDoesNotSupportBindings_IsNotChecked()
    {
        // Text in a field that never goes through the binding evaluator (a script, say) can hold
        // "${ a | b }" for its own reasons; it must not block publishing.
        var automation = GivenDraft(new StepConfigurationBuilder()
            .WithActionAlias(NotesActionAlias).WithName("Notes").WithAlias("notes")
            .WithSetting("text", "plain")
            .WithSetting("code", "`${ flags | mask }`")
            .Build());

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_ListRowWithUnknownFilter_IsRejected()
    {
        var automation = GivenDraft(new StepConfigurationBuilder()
            .WithActionAlias(NotesActionAlias).WithName("Notes").WithAlias("notes")
            .WithSetting("text", "plain")
            .WithSetting("rows", new[] { new Dictionary<string, object?> { ["value"] = "${ trigger.name | lowercse }" } })
            .Build());

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Notes' (notes): Field 'Rows' uses the binding filter 'lowercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_InputMappingWithUnknownFilter_IsRejected()
    {
        var step = new StepConfigurationBuilder()
            .WithActionAlias(LogMessageAlias).WithName("Log message").WithAlias("logMessage")
            .WithSetting("message", "Hello")
            .WithInputMapping("name", "${ trigger.name | uppercse }")
            .Build();
        var automation = GivenDraft(step);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Log message' (logMessage): Input 'name' uses the binding filter 'uppercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_ControlFlowConditionWithUnknownFilter_IsRejected()
    {
        var step = new StepConfigurationBuilder()
            .WithActionAlias(IfAlias).WithName("Is Danish").WithAlias("isDanish")
            .WithSetting("conditions", Filter("${ trigger.country | lowercse }"))
            .Build();
        var automation = GivenDraft(step);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Is Danish' (isDanish): Field 'Conditions' uses the binding filter 'lowercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_TriggerConnectionFilterWithUnknownFilter_IsRejected()
    {
        var step = LogStep(("message", "Hello"));
        var automation = GivenDraft(step, builder => builder.WithConnection(Guid.Empty, step.Id, filter: Filter("${ trigger.country | lowercse }")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("The filter on the connection from the trigger to 'Log message' uses the binding filter 'lowercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_StepConnectionFilterWithUnknownFilter_IsRejected()
    {
        var first = LogStep(("message", "Hello"));
        var second = new StepConfigurationBuilder()
            .WithActionAlias(LogMessageAlias).WithName("Log again").WithAlias("logAgain")
            .WithSetting("message", "Again")
            .Build();
        var automation = GivenDraft(first, builder => builder
            .AddStep(second)
            .WithConnection(first.Id, second.Id, filter: Filter("${ steps.logMessage.text | lowercse }")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("The filter on the connection from 'Log message' to 'Log again' uses the binding filter 'lowercse', which does not exist.");
    }

    [Fact]
    public async Task Publish_SameUnknownFilterTwiceInOneField_IsReportedOnce()
    {
        var automation = GivenDraft(LogStep(("message", "${ trigger.a | uppercse } ${ trigger.b | uppercse }")));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.Count(e => e.Contains("'uppercse'")).ShouldBe(1);
    }

    private Automation GivenDraft(StepConfiguration step, Func<AutomationBuilder, AutomationBuilder>? configure = null)
    {
        var builder = new AutomationBuilder().AsDraft().WithManualTrigger().AddStep(step);
        var automation = (configure?.Invoke(builder) ?? builder).Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(automation);
        return automation;
    }

    private static StepConfiguration LogStep(params (string Key, object? Value)[] settings)
    {
        var builder = new StepConfigurationBuilder().WithActionAlias(LogMessageAlias).WithName("Log message").WithAlias("logMessage");
        foreach (var (key, value) in settings)
        {
            builder.WithSetting(key, value);
        }

        return builder.Build();
    }

    private static ConditionSet Filter(string left)
        => new()
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = left, Operator = ConditionOperator.Equals, RightOperand = "dk" }],
                },
            ],
        };

    public sealed class NotesRow
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class NotesSettings
    {
        [Field(Label = "Text", SupportsBindings = true)]
        public string Text { get; set; } = string.Empty;

        [Field(Label = "Code")]
        public string Code { get; set; } = string.Empty;

        [Field(Label = "Rows", SupportsBindings = true)]
        public List<NotesRow> Rows { get; set; } = [];
    }

    [Action(NotesActionAlias, "Notes")]
    private sealed class NotesAction(ActionInfrastructure infrastructure) : ActionBase<NotesSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
