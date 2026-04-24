using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Connectors.ServiceConnectors;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Tests.Unit.Connectors.ServiceConnectors;

public class UmbracoAutomateAutomationServiceConnectorTests
{
    private readonly Mock<IAutomationService> _automationServiceMock = new();
    private readonly Mock<IWorkspaceService> _workspaceServiceMock = new();
    private readonly Mock<ISensitiveSettingsStripper> _stripperMock = new();
    private readonly Mock<UmbracoAutomateDeploySettingsAccessor> _settingsAccessorMock;
    private readonly List<IAction> _registeredActions = [];
    private readonly List<ITrigger> _registeredTriggers = [];
    private readonly UmbracoAutomateAutomationServiceConnector _connector;

    public UmbracoAutomateAutomationServiceConnectorTests()
    {
        _settingsAccessorMock = new Mock<UmbracoAutomateDeploySettingsAccessor>(MockBehavior.Strict, null!);
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings());

        // Default stripper passes input through unchanged.
        _stripperMock.Setup(x => x.StripTrigger(It.IsAny<TriggerConfiguration?>()))
            .Returns<TriggerConfiguration?>(t => t);
        _stripperMock.Setup(x => x.StripSteps(It.IsAny<IEnumerable<StepConfiguration>>()))
            .Returns<IEnumerable<StepConfiguration>>(s => s.ToList());

        // Default registry: the aliases used by test fixtures are registered on the target.
        // Individual tests can clear or extend these to simulate missing provider packages.
        _registeredActions.Add(BuildActionMock("http"));
        _registeredActions.Add(BuildActionMock("delay"));
        _registeredTriggers.Add(BuildTriggerMock("webhook"));

        var actionCollection = new ActionCollection(() => _registeredActions);
        var triggerCollection = new TriggerCollection(() => _registeredTriggers);

        _connector = new UmbracoAutomateAutomationServiceConnector(
            _automationServiceMock.Object,
            _workspaceServiceMock.Object,
            actionCollection,
            triggerCollection,
            _stripperMock.Object,
            _settingsAccessorMock.Object);
    }

    private static IAction BuildActionMock(string alias)
    {
        var mock = new Mock<IAction>();
        mock.SetupGet(x => x.Alias).Returns(alias);
        return mock.Object;
    }

    private static ITrigger BuildTriggerMock(string alias)
    {
        var mock = new Mock<ITrigger>();
        mock.SetupGet(x => x.Alias).Returns(alias);
        return mock.Object;
    }

    private static Automation BuildAutomation(
        Guid? workspaceId = null,
        Guid? groupId = null,
        IList<StepConfiguration>? steps = null,
        TriggerConfiguration? trigger = null) => new()
    {
        Alias = "sendDailyDigest",
        Name = "Send daily digest",
        WorkspaceId = workspaceId ?? Guid.NewGuid(),
        GroupId = groupId,
        Trigger = trigger,
        Steps = steps ?? [],
    };

    [Fact]
    public async Task GetArtifactAsync_AddsWorkspaceDependency()
    {
        var workspaceId = Guid.NewGuid();
        var automation = BuildAutomation(workspaceId: workspaceId);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        artifact.WorkspaceUdi.EntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);
        artifact.WorkspaceUdi.Guid.ShouldBe(workspaceId);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.Workspace &&
            ((GuidUdi)d.Udi).Guid == workspaceId);
    }

    [Fact]
    public async Task GetArtifactAsync_WithGroup_AddsGroupDependency()
    {
        var groupId = Guid.NewGuid();
        var automation = BuildAutomation(groupId: groupId);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        artifact.GroupId.ShouldBe(groupId);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup &&
            ((GuidUdi)d.Udi).Guid == groupId);
    }

    [Fact]
    public async Task GetArtifactAsync_WithoutGroup_OmitsGroupDependency()
    {
        var automation = BuildAutomation();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        artifact.GroupId.ShouldBeNull();
        artifact.Dependencies.ShouldNotContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup);
    }

    [Fact]
    public async Task GetArtifactAsync_AddsConnectionDependencyPerDistinctStepConnection()
    {
        var connection1 = Guid.NewGuid();
        var connection2 = Guid.NewGuid();
        var automation = BuildAutomation(steps:
        [
            new StepConfiguration { ActionAlias = "http", Name = "Step 1", ConnectionId = connection1 },
            new StepConfiguration { ActionAlias = "http", Name = "Step 2", ConnectionId = connection1 },
            new StepConfiguration { ActionAlias = "http", Name = "Step 3", ConnectionId = connection2 },
            new StepConfiguration { ActionAlias = "delay", Name = "Step 4", ConnectionId = null },
        ]);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        var connectionDeps = artifact.Dependencies
            .Where(d => d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.Connection)
            .ToList();
        // connection1 is referenced twice but should only appear once.
        connectionDeps.Count.ShouldBe(2);
        connectionDeps.ShouldContain(d => ((GuidUdi)d.Udi).Guid == connection1);
        connectionDeps.ShouldContain(d => ((GuidUdi)d.Udi).Guid == connection2);
    }

    [Fact]
    public async Task GetArtifactAsync_InvokesSensitiveStripperForTriggerAndSteps()
    {
        var trigger = new TriggerConfiguration { TriggerAlias = "webhook" };
        var step = new StepConfiguration { ActionAlias = "http", Name = "Step 1" };
        var automation = BuildAutomation(trigger: trigger, steps: [step]);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        await _connector.GetArtifactAsync(udi, automation);

        _stripperMock.Verify(x => x.StripTrigger(trigger), Times.Once);
        _stripperMock.Verify(x => x.StripSteps(automation.Steps), Times.Once);
    }

    [Fact]
    public async Task GetArtifactAsync_SerializesStrippedTriggerAndSteps()
    {
        var originalTrigger = new TriggerConfiguration
        {
            TriggerAlias = "webhook",
            Settings = { ["ApiKey"] = "secret" },
        };
        var strippedTrigger = new TriggerConfiguration { TriggerAlias = "webhook" };
        var strippedStep = new StepConfiguration { ActionAlias = "http", Name = "Step 1" };

        _stripperMock.Setup(x => x.StripTrigger(originalTrigger)).Returns(strippedTrigger);
        _stripperMock.Setup(x => x.StripSteps(It.IsAny<IEnumerable<StepConfiguration>>()))
            .Returns(new List<StepConfiguration> { strippedStep });

        var automation = BuildAutomation(
            trigger: originalTrigger,
            steps: [new StepConfiguration { ActionAlias = "http", Name = "Step 1" }]);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        artifact.Trigger.ShouldNotBeNull();
        var serializedTrigger = artifact.Trigger.Value.Deserialize<TriggerConfiguration>();
        serializedTrigger.ShouldNotBeNull();
        serializedTrigger.Settings.ShouldNotContainKey("ApiKey");

        artifact.Steps.ShouldNotBeNull();
        var serializedSteps = artifact.Steps.Value.Deserialize<IList<StepConfiguration>>();
        serializedSteps.ShouldNotBeNull();
        serializedSteps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetArtifactAsync_CopiesCoreFields()
    {
        var automation = BuildAutomation();
        automation.Description = "Nightly digest email";
        automation.CanvasState = "{\"viewport\":1}";
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);

        var artifact = await _connector.GetArtifactAsync(udi, automation);

        artifact.ShouldNotBeNull();
        artifact.Alias.ShouldBe("sendDailyDigest");
        artifact.Name.ShouldBe("Send daily digest");
        artifact.Description.ShouldBe("Nightly digest email");
        artifact.CanvasState.ShouldBe("{\"viewport\":1}");
    }

    [Fact]
    public async Task GetArtifactAsync_WithNullEntity_ReturnsNull()
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, Guid.NewGuid());

        var artifact = await _connector.GetArtifactAsync(udi, null);

        artifact.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntityAsync_DelegatesToAutomationService()
    {
        var id = Guid.NewGuid();
        var automation = BuildAutomation();
        _automationServiceMock
            .Setup(x => x.GetAutomationAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _connector.GetEntityAsync(id);

        result.ShouldBe(automation);
    }

    [Fact]
    public void GetEntityName_ReturnsAutomationName()
    {
        var automation = BuildAutomation();

        _connector.GetEntityName(automation).ShouldBe("Send daily digest");
    }

    [Fact]
    public void UdiEntityType_ReturnsAutomationUdiType()
    {
        _connector.UdiEntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.Automation);
    }

    [Fact]
    public async Task ProcessAsync_WithUnknownTriggerAlias_ThrowsWithActionableMessage()
    {
        _registeredTriggers.Clear();
        var workspaceId = Guid.NewGuid();
        _workspaceServiceMock
            .Setup(x => x.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Alias = "default", Name = "Default" });

        var artifact = BuildArtifact(
            workspaceId: workspaceId,
            trigger: new TriggerConfiguration { TriggerAlias = "slackMessageReceived" });
        var state = ArtifactDeployState.Create<AutomateAutomationArtifact, Automation>(artifact, null, _connector, 6);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _connector.ProcessAsync(state, Mock.Of<IDeployContext>(), 6));

        ex.Message.ShouldContain("'slackMessageReceived'");
        ex.Message.ShouldContain(artifact.Name);
        _automationServiceMock.Verify(
            x => x.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithUnknownStepActionAlias_ThrowsWithActionableMessage()
    {
        var workspaceId = Guid.NewGuid();
        _workspaceServiceMock
            .Setup(x => x.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Alias = "default", Name = "Default" });

        var artifact = BuildArtifact(
            workspaceId: workspaceId,
            trigger: new TriggerConfiguration { TriggerAlias = "webhook" },
            steps:
            [
                new StepConfiguration { ActionAlias = "slack.sendMessage", Name = "Notify team" },
            ]);
        var state = ArtifactDeployState.Create<AutomateAutomationArtifact, Automation>(artifact, null, _connector, 6);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _connector.ProcessAsync(state, Mock.Of<IDeployContext>(), 6));

        ex.Message.ShouldContain("'slack.sendMessage'");
        ex.Message.ShouldContain("Notify team");
        _automationServiceMock.Verify(
            x => x.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithRegisteredTriggerAndActions_CreatesAutomation()
    {
        var workspaceId = Guid.NewGuid();
        _workspaceServiceMock
            .Setup(x => x.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Alias = "default", Name = "Default" });
        _automationServiceMock
            .Setup(x => x.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var artifact = BuildArtifact(
            workspaceId: workspaceId,
            trigger: new TriggerConfiguration { TriggerAlias = "webhook" },
            steps:
            [
                new StepConfiguration { ActionAlias = "http", Name = "Call API" },
                new StepConfiguration { ActionAlias = "delay", Name = "Wait" },
            ]);
        var state = ArtifactDeployState.Create<AutomateAutomationArtifact, Automation>(artifact, null, _connector, 6);

        await _connector.ProcessAsync(state, Mock.Of<IDeployContext>(), 6);

        _automationServiceMock.Verify(
            x => x.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static AutomateAutomationArtifact BuildArtifact(
        Guid workspaceId,
        TriggerConfiguration? trigger = null,
        IList<StepConfiguration>? steps = null)
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, Guid.NewGuid());
        return new AutomateAutomationArtifact(udi)
        {
            Alias = "sendDailyDigest",
            Name = "Send daily digest",
            WorkspaceUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspaceId),
            Trigger = trigger is null ? null : JsonSerializer.SerializeToElement(trigger),
            Steps = steps is null || steps.Count == 0 ? null : JsonSerializer.SerializeToElement(steps),
        };
    }
}
