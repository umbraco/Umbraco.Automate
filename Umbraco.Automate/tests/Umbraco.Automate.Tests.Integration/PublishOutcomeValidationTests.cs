// S5 — Stale lines are caught before publish (docs/plans/action-outcomes/STORIES.md)

using System.Data;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for the publish-only stale-outcome rule in <see cref="AutomationService"/>,
/// using a mock repository and real test actions that declare dynamic outcomes.
/// </summary>
public class PublishOutcomeValidationTests
{
    private const string StaleLineError =
        "Step 'Decide' has a connection from outcome 'b', which the step no longer has. Reconnect or remove it.";

    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly AutomationService _service;

    public PublishOutcomeValidationTests()
    {
        var scopeProvider = new Mock<ICoreScopeProvider>();
        var scope = new Mock<ICoreScope>();
        scope.Setup(s => s.Notifications).Returns(Mock.Of<IScopedNotificationPublisher>());
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

        _repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));

        var resolver = new Mock<IEditableModelResolver>();
        resolver.Setup(r => r.ResolveModel<OptionsSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns((string _, object? data, EditableModelSchema? _) => new OptionsSettings
            {
                Options = data is Dictionary<string, object?> dict
                    && dict.TryGetValue("options", out var v)
                    && v is List<string> list ? list : [],
            });
        var actionDeps = new ActionInfrastructure(resolver.Object);

        // Settings of the static action can't be resolved: publish must not try.
        var throwingResolver = new Mock<IEditableModelResolver>();
        throwingResolver.Setup(r => r.ResolveModel<OptionsSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Throws(new FormatException("Saved settings are invalid."));

        var actions = new ActionCollection(() =>
        [
            new DecideAction(actionDeps),
            new EmptyOutcomesAction(actionDeps),
            new ThrowingOutcomesAction(actionDeps),
            new NullItemOutcomesAction(actionDeps),
            new DuplicateKeyOutcomesAction(actionDeps),
            new StaticOutcomesAction(new ActionInfrastructure(throwingResolver.Object)),
            new ThrowingStaticOutcomesAction(actionDeps),
        ]);
        var triggers = new TriggerCollection(() => []);
        var controlFlows = new ControlFlowCollection(() =>
            [new SwitchControlFlow(new ControlFlowInfrastructure(Mock.Of<IEditableModelResolver>()))]);
        var connectionTypes = new ConnectionTypeCollection(() => []);

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
                actions,
                triggers,
                controlFlows,
                connectionTypes,
                new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>),
                new NotificationChannelCollection(Array.Empty<INotificationChannel>)),
            new SectionAccessChecker());
    }

    #region Given a step "Decide" with a stale line from outcome "b"

    [Fact]
    public async Task SaveDraft_StaleOutcomeLine_Succeeds()
    {
        var automation = SetupAutomation(DecideWithLines(options: ["a"], "b"));

        await Should.NotThrowAsync(() => _service.UpdateAutomationAsync(automation));
    }

    [Fact]
    public async Task SaveDraft_StaleOutcomeLine_KeepsTheConnection()
    {
        var automation = SetupAutomation(DecideWithLines(options: ["a"], "b"));

        var saved = await _service.UpdateAutomationAsync(automation);

        saved.Connections.ShouldContain(c => c.Outcome == "b");
    }

    [Fact]
    public async Task Publish_StaleLineMovedToA_Succeeds()
    {
        var automation = SetupAutomation(DecideWithLines(options: ["a"], "a"));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    #endregion

    #region Sad path: publish blocked

    [Fact]
    public async Task Publish_StaleOutcomeLine_FailsWithStepAndOutcomeMessage()
    {
        var automation = SetupAutomation(DecideWithLines(options: ["a"], "b"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(StaleLineError);
    }

    [Fact]
    public async Task Publish_TwoStaleLines_ReportsTwoErrors()
    {
        var automation = SetupAutomation(DecideWithLines(options: ["a"], "b", "c"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.Count(e => e.Contains("which the step no longer has")).ShouldBe(2);
    }

    [Fact]
    public async Task Publish_EmptyDynamicListWithNamedLine_FailsWithStaleOutcomeError()
    {
        var automation = SetupAutomation(StepWithLines("test.emptyOutcomes", [], "a"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Decide' has a connection from outcome 'a', which the step no longer has. Reconnect or remove it.");
    }

    [Fact]
    public async Task Publish_GetOutcomesAsyncThrows_FailsWithCouldNotListOutcomesError()
    {
        var automation = SetupAutomation(StepWithLines("test.throwingOutcomes", [], "a"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain($"Step 'Decide' could not list its outcomes: {ThrowingOutcomesAction.Message}");
    }

    [Fact]
    public async Task Publish_StaticGetOutcomesThrows_FailsWithCouldNotListOutcomesError()
    {
        var automation = SetupAutomation(StepWithLines("test.throwingStaticOutcomes", [], "a"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain($"Step 'Decide' could not list its outcomes: {ThrowingStaticOutcomesAction.Message}");
    }

    [Fact]
    public async Task Publish_NullOutcomeInList_FailsWithStepPrefixedValidatorError()
    {
        var automation = SetupAutomation(StepWithLines("test.nullItemOutcomes", [], "a"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Decide': Outcome #2 is null.");
    }

    [Fact]
    public async Task Publish_DuplicateOutcomeKey_FailsWithStepPrefixedValidatorError()
    {
        var automation = SetupAutomation(StepWithLines("test.duplicateOutcomes", [], "a"));

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => _service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain("Step 'Decide': Outcome key 'a' is declared more than once.");
    }

    #endregion

    #region Lines this rule must not flag

    [Fact]
    public async Task Publish_StaticOutcomeActionWithUnresolvableSettings_StillPublishes()
    {
        var automation = SetupAutomation(StepWithLines("test.staticOutcomes", [], "a"));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_SwitchCaseLine_IsNotFlagged()
    {
        var automation = SetupAutomation(StepWithLines("umbracoAutomate.switch", [], "case-1"));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_UnnamedLineFromDeclaringStep_IsNotFlagged()
    {
        var automation = SetupAutomation(DecideWithLines(["a"], new string?[] { null }));

        var result = await _service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    #endregion

    private static AutomationBuilder DecideWithLines(string[] options, params string?[] outcomes)
        => StepWithLines("test.decide", options, outcomes);

    private static AutomationBuilder StepWithLines(string actionAlias, string[] options, params string?[] outcomes)
    {
        var decide = new StepConfigurationBuilder()
            .WithActionAlias(actionAlias)
            .WithName("Decide")
            .WithAlias("decide")
            .WithSetting("options", options.ToList())
            .Build();

        var builder = new AutomationBuilder().AsDraft().WithManualTrigger().AddStep(decide);
        foreach (var outcome in outcomes)
        {
            var target = new StepConfigurationBuilder()
                .WithActionAlias("test.target")
                .WithName($"Target {outcome ?? "any"}")
                .WithAlias($"target{Guid.NewGuid():N}")
                .Build();
            builder.AddStep(target).WithConnection(decide.Id, target.Id, outcome);
        }

        return builder;
    }

    private Automation SetupAutomation(AutomationBuilder builder)
    {
        var automation = builder.Build();
        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        return automation;
    }

    #region Test step types

    private sealed class OptionsSettings
    {
        public List<string> Options { get; set; } = [];
    }

    [Action("test.decide", "Decide")]
    private sealed class DecideAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StepOutcome>>(
                (settings?.Options ?? []).Select(o => new StepOutcome(o, o)).ToList());
    }

    [Action("test.emptyOutcomes", "Empty Outcomes")]
    private sealed class EmptyOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StepOutcome>>([]);
    }

    [Action("test.throwingOutcomes", "Throwing Outcomes")]
    private sealed class ThrowingOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public const string Message = "The option source is unavailable.";

        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
            => throw new InvalidOperationException(Message);
    }

    [Action("test.throwingStaticOutcomes", "Throwing Static Outcomes")]
    private sealed class ThrowingStaticOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public const string Message = "The static declaration is broken.";

        public override IReadOnlyList<StepOutcome> GetOutcomes() => throw new InvalidOperationException(Message);

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.nullItemOutcomes", "Null Item Outcomes")]
    private sealed class NullItemOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StepOutcome>>([new StepOutcome("a", "A"), null!]);
    }

    [Action("test.duplicateOutcomes", "Duplicate Outcomes")]
    private sealed class DuplicateKeyOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OptionsSettings? settings, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<StepOutcome>>([new StepOutcome("a", "A"), new StepOutcome("a", "A again")]);
    }

    [Action("test.staticOutcomes", "Static Outcomes")]
    private sealed class StaticOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<OptionsSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() => [new StepOutcome("a", "A")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
