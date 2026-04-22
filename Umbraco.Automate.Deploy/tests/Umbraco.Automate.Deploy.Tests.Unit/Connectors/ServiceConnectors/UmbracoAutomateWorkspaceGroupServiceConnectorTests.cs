using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Connectors.ServiceConnectors;
using Umbraco.Cms.Core;

namespace Umbraco.Automate.Deploy.Tests.Unit.Connectors.ServiceConnectors;

public class UmbracoAutomateWorkspaceGroupServiceConnectorTests
{
    private readonly Mock<IWorkspaceGroupService> _groupServiceMock = new();
    private readonly Mock<UmbracoAutomateDeploySettingsAccessor> _settingsAccessorMock;
    private readonly UmbracoAutomateWorkspaceGroupServiceConnector _connector;

    public UmbracoAutomateWorkspaceGroupServiceConnectorTests()
    {
        _settingsAccessorMock = new Mock<UmbracoAutomateDeploySettingsAccessor>(MockBehavior.Strict, null!);
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings());

        _connector = new UmbracoAutomateWorkspaceGroupServiceConnector(
            _groupServiceMock.Object,
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
}
