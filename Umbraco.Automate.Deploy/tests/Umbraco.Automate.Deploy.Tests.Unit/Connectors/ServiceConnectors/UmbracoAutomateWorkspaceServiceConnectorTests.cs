using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Connectors.ServiceConnectors;
using Umbraco.Cms.Core;

namespace Umbraco.Automate.Deploy.Tests.Unit.Connectors.ServiceConnectors;

public class UmbracoAutomateWorkspaceServiceConnectorTests
{
    private readonly Mock<IWorkspaceService> _workspaceServiceMock = new();
    private readonly Mock<IWorkspaceGroupService> _groupServiceMock = new();
    private readonly Mock<IAutomationService> _automationServiceMock = new();
    private readonly Mock<IConnectionService> _connectionServiceMock = new();
    private readonly Mock<UmbracoAutomateDeploySettingsAccessor> _settingsAccessorMock;
    private readonly UmbracoAutomateWorkspaceServiceConnector _connector;

    public UmbracoAutomateWorkspaceServiceConnectorTests()
    {
        _settingsAccessorMock = new Mock<UmbracoAutomateDeploySettingsAccessor>(MockBehavior.Strict, null!);
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings());

        _connector = new UmbracoAutomateWorkspaceServiceConnector(
            _workspaceServiceMock.Object,
            _groupServiceMock.Object,
            _automationServiceMock.Object,
            _connectionServiceMock.Object,
            _settingsAccessorMock.Object);
    }

    private static Workspace BuildWorkspace(
        Guid? serviceAccountKey = null,
        IList<Guid>? userGroups = null,
        IList<Guid>? allowedConnections = null) => new()
    {
        Alias = "marketing",
        Name = "Marketing",
        ServiceAccountKey = serviceAccountKey ?? Guid.NewGuid(),
        UserGroups = userGroups ?? [],
        AllowedConnections = allowedConnections ?? [],
    };

    [Fact]
    public async Task GetArtifactAsync_CopiesCoreFields()
    {
        var serviceAccountKey = Guid.NewGuid();
        var workspace = BuildWorkspace(serviceAccountKey: serviceAccountKey);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspace.Id);

        var artifact = await _connector.GetArtifactAsync(udi, workspace);

        artifact.ShouldNotBeNull();
        artifact.Alias.ShouldBe("marketing");
        artifact.Name.ShouldBe("Marketing");
        artifact.ServiceAccountKey.ShouldBe(serviceAccountKey);
    }

    [Fact]
    public async Task GetArtifactAsync_AddsServiceAccountUserDependency()
    {
        var serviceAccountKey = Guid.NewGuid();
        var workspace = BuildWorkspace(serviceAccountKey: serviceAccountKey);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspace.Id);

        var artifact = await _connector.GetArtifactAsync(udi, workspace);

        artifact.ShouldNotBeNull();
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == Umbraco.Cms.Core.Constants.UdiEntityType.User &&
            ((GuidUdi)d.Udi).Guid == serviceAccountKey);
    }

    [Fact]
    public async Task GetArtifactAsync_AddsUserGroupDependencyPerGroup()
    {
        var group1 = Guid.NewGuid();
        var group2 = Guid.NewGuid();
        var workspace = BuildWorkspace(userGroups: [group1, group2]);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspace.Id);

        var artifact = await _connector.GetArtifactAsync(udi, workspace);

        artifact.ShouldNotBeNull();
        artifact.UserGroups.ShouldBe(new[] { group1, group2 });
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == Umbraco.Cms.Core.Constants.UdiEntityType.UserGroup &&
            ((GuidUdi)d.Udi).Guid == group1);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == Umbraco.Cms.Core.Constants.UdiEntityType.UserGroup &&
            ((GuidUdi)d.Udi).Guid == group2);
    }

    [Fact]
    public async Task GetArtifactAsync_AddsConnectionDependencyPerAllowedConnection()
    {
        var connection1 = Guid.NewGuid();
        var connection2 = Guid.NewGuid();
        var workspace = BuildWorkspace(allowedConnections: [connection1, connection2]);
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspace.Id);

        var artifact = await _connector.GetArtifactAsync(udi, workspace);

        artifact.ShouldNotBeNull();
        artifact.AllowedConnectionUdis.Count.ShouldBe(2);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.Connection &&
            ((GuidUdi)d.Udi).Guid == connection1);
        artifact.Dependencies.ShouldContain(d =>
            d.Udi.EntityType == UmbracoAutomateDeployConstants.UdiEntityType.Connection &&
            ((GuidUdi)d.Udi).Guid == connection2);
    }

    [Fact]
    public async Task GetArtifactAsync_WithNoConnectionsOrGroups_StillAddsServiceAccountDependency()
    {
        var workspace = BuildWorkspace();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, workspace.Id);

        var artifact = await _connector.GetArtifactAsync(udi, workspace);

        artifact.ShouldNotBeNull();
        artifact.Dependencies.Count().ShouldBe(1);
        artifact.Dependencies.Single().Udi.EntityType.ShouldBe(Umbraco.Cms.Core.Constants.UdiEntityType.User);
    }

    [Fact]
    public async Task GetArtifactAsync_WithNullEntity_ReturnsNull()
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, Guid.NewGuid());

        var artifact = await _connector.GetArtifactAsync(udi, null);

        artifact.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntityAsync_DelegatesToWorkspaceService()
    {
        var id = Guid.NewGuid();
        var workspace = BuildWorkspace();
        _workspaceServiceMock
            .Setup(x => x.GetWorkspaceAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        var result = await _connector.GetEntityAsync(id);

        result.ShouldBe(workspace);
    }

    [Fact]
    public void GetEntityName_ReturnsWorkspaceName()
    {
        var workspace = BuildWorkspace();

        _connector.GetEntityName(workspace).ShouldBe("Marketing");
    }

    [Fact]
    public void UdiEntityType_ReturnsWorkspaceUdiType()
    {
        _connector.UdiEntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);
    }
}
