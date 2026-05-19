using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.HealthChecks;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.HealthChecks;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.HealthChecks;

public class AutomationPermissionDriftHealthCheckTests
{
    private static readonly TriggerInfrastructure TriggerDeps = new(Mock.Of<IEditableModelResolver>());
    private static readonly ActionInfrastructure ActionDeps = new(Mock.Of<IEditableModelResolver>());

    [Fact]
    public async Task Returns_success_when_there_are_no_published_automations()
    {
        var check = BuildCheck(automations: [], triggers: [], actions: []);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Success);
    }

    [Fact]
    public async Task Returns_success_when_all_automations_satisfy_their_section_requirements()
    {
        var workspace = WorkspaceWithSections("content");
        var automation = PublishedAutomation(workspace.Id, "test.contentTrigger", ["test.contentAction"]);

        var check = BuildCheck(
            automations: [automation],
            workspaces: [workspace],
            user: UserWithSections("content"),
            triggers: [new ContentTrigger(TriggerDeps)],
            actions: [new ContentAction(ActionDeps)]);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Success);
    }

    [Fact]
    public async Task Returns_warning_when_trigger_section_is_missing()
    {
        var workspace = WorkspaceWithSections();
        var automation = PublishedAutomation(workspace.Id, "test.contentTrigger", []);

        var check = BuildCheck(
            automations: [automation],
            workspaces: [workspace],
            user: UserWithSections("media"),
            triggers: [new ContentTrigger(TriggerDeps)],
            actions: []);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Warning);
        status.Message.ShouldContain("Content Trigger");
        status.Message.ShouldContain(automation.Name);
    }

    [Fact]
    public async Task Returns_warning_when_step_action_section_is_missing()
    {
        var workspace = WorkspaceWithSections();
        var automation = PublishedAutomation(workspace.Id, "test.universalTrigger", ["test.mediaAction"]);

        var check = BuildCheck(
            automations: [automation],
            workspaces: [workspace],
            user: UserWithSections("content"),
            triggers: [new UniversalTrigger(TriggerDeps)],
            actions: [new MediaAction(ActionDeps)]);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Warning);
        status.Message.ShouldContain("Media Action");
    }

    [Fact]
    public async Task Aggregates_multiple_violations()
    {
        var workspace = WorkspaceWithSections();
        var automationA = PublishedAutomation(workspace.Id, "test.contentTrigger", []);
        automationA.Name = "Automation A";
        var automationB = PublishedAutomation(workspace.Id, "test.universalTrigger", ["test.mediaAction"]);
        automationB.Name = "Automation B";

        var check = BuildCheck(
            automations: [automationA, automationB],
            workspaces: [workspace],
            user: UserWithSections("users"),
            triggers: [new ContentTrigger(TriggerDeps), new UniversalTrigger(TriggerDeps)],
            actions: [new MediaAction(ActionDeps)]);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Warning);
        status.Message.ShouldContain("Automation A");
        status.Message.ShouldContain("Automation B");
    }

    [Fact]
    public async Task Reports_when_service_account_user_is_missing()
    {
        var workspace = WorkspaceWithSections();
        var automation = PublishedAutomation(workspace.Id, "test.universalTrigger", []);

        var check = BuildCheck(
            automations: [automation],
            workspaces: [workspace],
            user: null,
            triggers: [new UniversalTrigger(TriggerDeps)],
            actions: []);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Warning);
        status.Message.ShouldContain("service account");
        status.Message.ShouldContain("not found");
    }

    private static Workspace WorkspaceWithSections(params string[] _)
        => new WorkspaceBuilder().Build();

    private static IUser UserWithSections(params string[] sections)
        => Mock.Of<IUser>(u => u.AllowedSections == sections);

    private static Automation PublishedAutomation(Guid workspaceId, string triggerAlias, string[] actionAliases)
    {
        var automation = new AutomationBuilder().AsDraft().Build();
        automation.Status = AutomationStatus.Published;
        automation.WorkspaceId = workspaceId;
        automation.Trigger = new TriggerConfiguration
        {
            TriggerAlias = triggerAlias,
            Settings = new Dictionary<string, object?>(),
        };
        automation.Steps = actionAliases.Select((alias, i) => new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = alias,
            Name = $"Step {i + 1}",
            Alias = $"step{i + 1}",
            Settings = new Dictionary<string, object?>(),
        }).ToList();
        return automation;
    }

    private static AutomationPermissionDriftHealthCheck BuildCheck(
        Automation[] automations,
        ITrigger[] triggers,
        IAction[] actions,
        Workspace[]? workspaces = null,
        IUser? user = null)
    {
        var automationService = new Mock<IAutomationService>();
        automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(automations);

        var workspaceService = new Mock<IWorkspaceService>();
        foreach (var workspace in workspaces ?? [])
        {
            workspaceService.Setup(s => s.GetWorkspaceAsync(workspace.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(workspace);
        }

        var serviceAccountResolver = new Mock<IWorkspaceServiceAccountResolver>();
        serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        return new AutomationPermissionDriftHealthCheck(
            automationService.Object,
            workspaceService.Object,
            serviceAccountResolver.Object,
            new TriggerCollection(() => triggers),
            new ActionCollection(() => actions),
            new SectionAccessChecker(),
            NullLogger<AutomationPermissionDriftHealthCheck>.Instance);
    }

    #region Test step types

    [Trigger("test.contentTrigger", "Content Trigger", RequiredSections = ["content"])]
    private sealed class ContentTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    [Trigger("test.universalTrigger", "Universal Trigger")]
    private sealed class UniversalTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    [Action("test.contentAction", "Content Action", RequiredSections = ["content"])]
    private sealed class ContentAction(ActionInfrastructure infrastructure) : ActionBase<object, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.mediaAction", "Media Action", RequiredSections = ["media"])]
    private sealed class MediaAction(ActionInfrastructure infrastructure) : ActionBase<object, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
