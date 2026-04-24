using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Connectors.ServiceConnectors;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Tests.Unit.Connectors.ServiceConnectors;

public class UmbracoAutomateWorkspaceGroupServiceConnectorTests
{
    private readonly Mock<IWorkspaceGroupService> _groupServiceMock = new();
    private readonly Mock<IAutomationService> _automationServiceMock = new();
    private readonly Mock<UmbracoAutomateDeploySettingsAccessor> _settingsAccessorMock;
    private readonly UmbracoAutomateWorkspaceGroupServiceConnector _connector;

    public UmbracoAutomateWorkspaceGroupServiceConnectorTests()
    {
        _settingsAccessorMock = new Mock<UmbracoAutomateDeploySettingsAccessor>(MockBehavior.Strict, null!);
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings());

        _connector = new UmbracoAutomateWorkspaceGroupServiceConnector(
            _groupServiceMock.Object,
            _automationServiceMock.Object,
            _settingsAccessorMock.Object);
    }

    private static WorkspaceGroup BuildGroup(Guid? workspaceId = null, Guid? parentId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Campaigns",
        WorkspaceId = workspaceId ?? Guid.NewGuid(),
        ParentId = parentId,
    };

    [Fact]
    public async Task GetArtifactAsync_AddsWorkspaceDependency()
    {
        var workspaceId = Guid.NewGuid();
        var group = BuildGroup(workspaceId: workspaceId);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, group.Id);

        var artifact = await _connector.GetArtifactAsync(udi, group);

        artifact.ShouldNotBeNull();
        artifact.WorkspaceUdi.EntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);
        artifact.WorkspaceUdi.Guid.ShouldBe(workspaceId);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.Workspace &&
            ((GuidUdi)d.Udi).Guid == workspaceId);
    }

    [Fact]
    public async Task GetArtifactAsync_WithParent_AddsParentGroupDependency()
    {
        var parentId = Guid.NewGuid();
        var group = BuildGroup(parentId: parentId);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, group.Id);

        var artifact = await _connector.GetArtifactAsync(udi, group);

        artifact.ShouldNotBeNull();
        artifact.ParentUdi.ShouldNotBeNull();
        artifact.ParentUdi.Guid.ShouldBe(parentId);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup &&
            ((GuidUdi)d.Udi).Guid == parentId);
    }

    [Fact]
    public async Task GetArtifactAsync_WithoutParent_OmitsParentDependency()
    {
        var group = BuildGroup();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, group.Id);

        var artifact = await _connector.GetArtifactAsync(udi, group);

        artifact.ShouldNotBeNull();
        artifact.ParentUdi.ShouldBeNull();
        artifact.Dependencies.ShouldNotContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup);
    }

    [Fact]
    public async Task GetArtifactAsync_CopiesName_AndLeavesAliasUnset()
    {
        var group = BuildGroup();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, group.Id);

        var artifact = await _connector.GetArtifactAsync(udi, group);

        artifact.ShouldNotBeNull();
        artifact.Name.ShouldBe("Campaigns");
        // Groups have no alias; we intentionally leave it unset so the serialized
        // artifact doesn't carry a misleading "Alias" field derived from Name.
        artifact.Alias.ShouldBeNull();
    }

    [Fact]
    public async Task GetArtifactAsync_WithNullEntity_ReturnsNull()
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, Guid.NewGuid());

        var artifact = await _connector.GetArtifactAsync(udi, null);

        artifact.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntityAsync_DelegatesToGroupService()
    {
        var id = Guid.NewGuid();
        var group = BuildGroup();
        _groupServiceMock
            .Setup(x => x.GetGroupAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _connector.GetEntityAsync(id);

        result.ShouldBe(group);
    }

    [Fact]
    public void GetEntityName_ReturnsGroupName()
    {
        var group = BuildGroup();

        _connector.GetEntityName(group).ShouldBe("Campaigns");
    }

    [Fact]
    public void UdiEntityType_ReturnsWorkspaceGroupUdiType()
    {
        _connector.UdiEntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup);
    }

    [Fact]
    public async Task ProcessAsync_Pass4_WhenNewGroupWithParent_CreatesAtRootDeferringReparent()
    {
        // Pass 4 must land the group at the root regardless of the artifact's ParentUdi,
        // because Deploy processes artifacts within a pass in package order and the parent
        // may not exist yet. Reparenting happens in pass 5.
        var workspaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var artifact = BuildArtifact(groupId, workspaceId, parentId);
        var state = BuildState(artifact, entity: null, pass: 4);

        var saved = new WorkspaceGroup { Id = groupId, Name = "Campaigns", WorkspaceId = workspaceId };
        _groupServiceMock
            .Setup(x => x.SaveGroupForDeployAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        await _connector.ProcessAsync(state, context: null!, pass: 4);

        _groupServiceMock.Verify(x => x.SaveGroupForDeployAsync(
            It.Is<WorkspaceGroup>(g =>
                g.Id == groupId &&
                g.WorkspaceId == workspaceId &&
                g.ParentId == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
        state.Entity.ShouldBe(saved);
    }

    [Fact]
    public async Task ProcessAsync_Pass4_WhenExistingGroupWithParent_ResetsToRoot()
    {
        // Updating an existing nested group must also drop it to root in pass 4 so
        // pass 5's reparent has a consistent starting point.
        var workspaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        var existing = new WorkspaceGroup
        {
            Id = groupId,
            Name = "Old Name",
            WorkspaceId = workspaceId,
            ParentId = Guid.NewGuid(),
        };
        var artifact = BuildArtifact(groupId, workspaceId, parentId, name: "Campaigns");
        var state = BuildState(artifact, entity: existing, pass: 4);

        _groupServiceMock
            .Setup(x => x.SaveGroupForDeployAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        await _connector.ProcessAsync(state, context: null!, pass: 4);

        _groupServiceMock.Verify(x => x.SaveGroupForDeployAsync(
            It.Is<WorkspaceGroup>(g =>
                g.Id == groupId &&
                g.Name == "Campaigns" &&
                g.WorkspaceId == workspaceId &&
                g.ParentId == null),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Pass5_WithParentUdi_Reparents()
    {
        var workspaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var parentId = Guid.NewGuid();

        // Pass 5 runs on the entity produced by pass 4 — start from a root-landed group.
        var rootLanded = new WorkspaceGroup
        {
            Id = groupId,
            Name = "Campaigns",
            WorkspaceId = workspaceId,
            ParentId = null,
        };
        var artifact = BuildArtifact(groupId, workspaceId, parentId);
        var state = BuildState(artifact, entity: rootLanded, pass: 5);

        _groupServiceMock
            .Setup(x => x.SaveGroupForDeployAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        await _connector.ProcessAsync(state, context: null!, pass: 5);

        _groupServiceMock.Verify(x => x.SaveGroupForDeployAsync(
            It.Is<WorkspaceGroup>(g =>
                g.Id == groupId &&
                g.ParentId == parentId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_Pass5_WithoutParentUdi_IsNoOp()
    {
        // Root groups have no ParentUdi on the artifact — pass 5 must not touch them,
        // otherwise we'd issue a second save per group for no reason.
        var workspaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var rootLanded = new WorkspaceGroup
        {
            Id = groupId,
            Name = "Campaigns",
            WorkspaceId = workspaceId,
            ParentId = null,
        };
        var artifact = BuildArtifact(groupId, workspaceId, parentId: null);
        var state = BuildState(artifact, entity: rootLanded, pass: 5);

        await _connector.ProcessAsync(state, context: null!, pass: 5);

        _groupServiceMock.Verify(x => x.SaveGroupForDeployAsync(
            It.IsAny<WorkspaceGroup>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AutomateWorkspaceGroupArtifact BuildArtifact(
        Guid groupId,
        Guid workspaceId,
        Guid? parentId,
        string name = "Campaigns")
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, groupId);
        var workspaceUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspaceId);
        var parentUdi = parentId.HasValue
            ? new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, parentId.Value)
            : null;

        return new AutomateWorkspaceGroupArtifact(udi)
        {
            Name = name,
            WorkspaceUdi = workspaceUdi,
            ParentUdi = parentUdi,
        };
    }

    private static ArtifactDeployState<AutomateWorkspaceGroupArtifact, WorkspaceGroup> BuildState(
        AutomateWorkspaceGroupArtifact artifact,
        WorkspaceGroup? entity,
        int pass)
        => new(artifact, entity, connector: null!, pass);
}
