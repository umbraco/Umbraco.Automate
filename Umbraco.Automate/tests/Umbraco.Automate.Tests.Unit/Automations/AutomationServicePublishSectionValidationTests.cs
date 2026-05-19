using System.Data;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class AutomationServicePublishSectionValidationTests
{
    private static readonly TriggerInfrastructure TriggerDeps = new(Mock.Of<IEditableModelResolver>());
    private static readonly ActionInfrastructure ActionDeps = new(Mock.Of<IEditableModelResolver>());

    [Fact]
    public async Task Publish_succeeds_when_service_account_satisfies_all_section_requirements()
    {
        var (service, repo) = BuildService(
            triggers: [new ContentTrigger(TriggerDeps)],
            actions: [new ContentAction(ActionDeps)],
            allowedSections: ["content"]);

        var automation = SetupAutomation(repo, "test.contentTrigger", ["test.contentAction"]);

        var result = await service.PublishAutomationAsync(automation.Id);

        result.Status.ShouldBe(AutomationStatus.Published);
    }

    [Fact]
    public async Task Publish_fails_when_trigger_requires_section_account_lacks()
    {
        var (service, repo) = BuildService(
            triggers: [new ContentTrigger(TriggerDeps)],
            actions: [],
            allowedSections: ["media"]);

        var automation = SetupAutomation(repo, "test.contentTrigger", []);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.Contains("Content Trigger") && e.Contains("content"));
    }

    [Fact]
    public async Task Publish_fails_when_action_requires_section_account_lacks()
    {
        var (service, repo) = BuildService(
            triggers: [new UniversalTrigger(TriggerDeps)],
            actions: [new MediaAction(ActionDeps)],
            allowedSections: ["content"]);

        var automation = SetupAutomation(repo, "test.universalTrigger", ["test.mediaAction"]);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e =>
            e.Contains("uses action 'Media Action'")
            && e.Contains("media"));
    }

    [Fact]
    public async Task Publish_aggregates_trigger_and_action_section_errors()
    {
        var (service, repo) = BuildService(
            triggers: [new ContentTrigger(TriggerDeps)],
            actions: [new MediaAction(ActionDeps)],
            allowedSections: ["users"]);

        var automation = SetupAutomation(repo, "test.contentTrigger", ["test.mediaAction"]);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => service.PublishAutomationAsync(automation.Id));

        ex.Errors.Count(e => e.Contains("Content Trigger")).ShouldBe(1);
        ex.Errors.Count(e => e.Contains("Media Action")).ShouldBe(1);
    }

    [Fact]
    public async Task Publish_fails_when_service_account_lookup_returns_null()
    {
        var serviceAccountKey = Guid.NewGuid();
        var (service, repo) = BuildService(
            triggers: [new UniversalTrigger(TriggerDeps)],
            actions: [],
            serviceAccountKey: serviceAccountKey,
            user: null);

        var automation = SetupAutomation(repo, "test.universalTrigger", []);

        var ex = await Should.ThrowAsync<AutomationValidationException>(
            () => service.PublishAutomationAsync(automation.Id));

        ex.Errors.ShouldContain(e => e.Contains("Service account") && e.Contains("not found"));
    }

    private static Automation SetupAutomation(Mock<IAutomationRepository> repo, string triggerAlias, string[] actionAliases)
    {
        var automation = new AutomationBuilder()
            .AsDraft()
            .Build();

        automation.Trigger = new TriggerConfiguration
        {
            TriggerAlias = triggerAlias,
            Settings = new Dictionary<string, object?>(),
        };

        automation.Steps = actionAliases
            .Select((alias, index) => new StepConfiguration
            {
                Id = Guid.NewGuid(),
                ActionAlias = alias,
                Name = $"Step {index + 1}",
                Alias = $"step{index + 1}",
                Settings = new Dictionary<string, object?>(),
            })
            .ToList();

        repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        repo.Setup(r => r.SaveMetadataAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        return automation;
    }

    private static (AutomationService Service, Mock<IAutomationRepository> Repo) BuildService(
        ITrigger[] triggers,
        IAction[] actions,
        string[]? allowedSections = null,
        Guid? serviceAccountKey = null,
        IUser? user = null)
    {
        var repo = new Mock<IAutomationRepository>();
        var workspaceService = new Mock<IWorkspaceService>();
        var userService = new Mock<IUserService>();
        var scopeProvider = new Mock<ICoreScopeProvider>();
        var scope = new Mock<ICoreScope>();
        var notifications = new Mock<IScopedNotificationPublisher>();

        scope.Setup(s => s.Notifications).Returns(notifications.Object);
        scopeProvider.Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(scope.Object);

        var key = serviceAccountKey ?? Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithServiceAccountKey(key)
            .Build();

        workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var resolvedUser = user;
        if (resolvedUser is null && allowedSections is not null)
        {
            resolvedUser = Mock.Of<IUser>(u => u.AllowedSections == allowedSections);
        }
        userService.Setup(u => u.GetAsync(key)).ReturnsAsync(resolvedUser);

        var actionCollection = new ActionCollection(() => actions);
        var triggerCollection = new TriggerCollection(() => triggers);
        var controlFlowCollection = new ControlFlowCollection(() => []);
        var connectionTypeCollection = new ConnectionTypeCollection(() => []);

        var service = new AutomationService(
            repo.Object,
            Mock.Of<IAutomationRunRepository>(),
            Mock.Of<IEntityVersionService>(),
            workspaceService.Object,
            Mock.Of<IConnectionService>(),
            userService.Object,
            scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            actionCollection,
            triggerCollection,
            controlFlowCollection,
            new SensitiveSettingsStripper(actionCollection, triggerCollection, controlFlowCollection, connectionTypeCollection),
            new SectionAccessChecker());

        return (service, repo);
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
