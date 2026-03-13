using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Tests.Common.Builders;

namespace Umbraco.Automate.Tests.Unit.Workspaces;

public class WorkspaceVersionableEntityAdapterTests
{
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly WorkspaceVersionableEntityAdapter _adapter;

    public WorkspaceVersionableEntityAdapterTests()
    {
        _adapter = new WorkspaceVersionableEntityAdapter(
            _workspaceService.Object,
            Mock.Of<ILogger<WorkspaceVersionableEntityAdapter>>());
    }

    [Fact]
    public void EntityTypeName_ReturnsWorkspace()
        => _adapter.EntityTypeName.ShouldBe("Workspace");

    [Fact]
    public void SnapshotRoundtrip_PreservesFields()
    {
        var groupId = Guid.NewGuid();
        var connId = Guid.NewGuid();
        Workspace workspace = new WorkspaceBuilder()
            .WithName("My Workspace")
            .WithAlias("my-workspace")
            .WithUserGroups(groupId)
            .WithAllowedConnections(connId);

        IVersionableEntityAdapter adapter = _adapter;
        var json = adapter.CreateSnapshot(workspace);
        var restored = (Workspace?)adapter.RestoreFromSnapshot(json);

        restored.ShouldNotBeNull();
        restored.Name.ShouldBe("My Workspace");
        restored.Alias.ShouldBe("my-workspace");
        restored.UserGroups.ShouldContain(groupId);
        restored.AllowedConnections.ShouldContain(connId);
    }

    [Fact]
    public void CompareVersions_DetectsNameChange()
    {
        Workspace from = new WorkspaceBuilder().WithName("Old");
        Workspace to = new WorkspaceBuilder().WithName("New");

        IVersionableEntityAdapter adapter = _adapter;
        var changes = adapter.CompareVersions(from, to);

        changes.ShouldContain(c => c.Path == "Name" && c.OldValue == "Old" && c.NewValue == "New");
    }

    [Fact]
    public void CompareVersions_DetectsAddedUserGroup()
    {
        var groupId = Guid.NewGuid();
        Workspace from = new WorkspaceBuilder().WithUserGroups();
        Workspace to = new WorkspaceBuilder().WithUserGroups(groupId);

        IVersionableEntityAdapter adapter = _adapter;
        var changes = adapter.CompareVersions(from, to);

        changes.ShouldContain(c => c.Path == "UserGroups" && c.OldValue == null && c.NewValue == groupId.ToString());
    }

    [Fact]
    public void CompareVersions_DetectsRemovedConnection()
    {
        var connId = Guid.NewGuid();
        Workspace from = new WorkspaceBuilder().WithAllowedConnections(connId);
        Workspace to = new WorkspaceBuilder().WithAllowedConnections();

        IVersionableEntityAdapter adapter = _adapter;
        var changes = adapter.CompareVersions(from, to);

        changes.ShouldContain(c => c.Path == "AllowedConnections" && c.OldValue == connId.ToString() && c.NewValue == null);
    }

    [Fact]
    public void CompareVersions_DetectsServiceAccountKeyChange()
    {
        var oldKey = Guid.NewGuid();
        var newKey = Guid.NewGuid();
        Workspace from = new WorkspaceBuilder().WithServiceAccountKey(oldKey);
        Workspace to = new WorkspaceBuilder().WithServiceAccountKey(newKey);

        IVersionableEntityAdapter adapter = _adapter;
        var changes = adapter.CompareVersions(from, to);

        changes.ShouldContain(c => c.Path == "ServiceAccountKey");
    }

    [Fact]
    public void CompareVersions_NoChanges_ReturnsEmpty()
    {
        var id = Guid.NewGuid();
        var saKey = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        Workspace from = new WorkspaceBuilder().WithId(id).WithName("Same").WithAlias("same").WithServiceAccountKey(saKey).WithUserGroups(groupId);
        Workspace to = new WorkspaceBuilder().WithId(id).WithName("Same").WithAlias("same").WithServiceAccountKey(saKey).WithUserGroups(groupId);

        IVersionableEntityAdapter adapter = _adapter;
        var changes = adapter.CompareVersions(from, to);

        changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task RollbackAsync_DelegatesToService()
    {
        var id = Guid.NewGuid();
        _workspaceService.Setup(s => s.RollbackWorkspaceAsync(id, 3, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder().Build());

        await _adapter.RollbackAsync(id, 3);

        _workspaceService.Verify(s => s.RollbackWorkspaceAsync(id, 3, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProtectedVersionsAsync_ReturnsEmpty()
    {
        var result = await _adapter.GetProtectedVersionsAsync();
        result.ShouldBeEmpty();
    }
}
