using Shouldly;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Workspaces;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class WorkspaceFactoryTests
{
    [Fact]
    public void BuildEntity_AndBuildDomain_RoundTrips()
    {
        var userGroup1 = Guid.NewGuid();
        var userGroup2 = Guid.NewGuid();
        var connection1 = Guid.NewGuid();

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Alias = "testWorkspace",
            Name = "Test Workspace",
            ServiceAccountKey = Guid.NewGuid(),
            UserGroups = [userGroup1, userGroup2],
            AllowedConnections = [connection1],
            Version = 3,
            DateCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateModified = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = Guid.NewGuid(),
            ModifiedByUserId = Guid.NewGuid(),
        };

        WorkspaceEntity entity = WorkspaceFactory.BuildEntity(workspace);
        Workspace roundTripped = WorkspaceFactory.BuildDomain(entity);

        roundTripped.Id.ShouldBe(workspace.Id);
        roundTripped.Alias.ShouldBe(workspace.Alias);
        roundTripped.Name.ShouldBe(workspace.Name);
        roundTripped.ServiceAccountKey.ShouldBe(workspace.ServiceAccountKey);
        roundTripped.UserGroups.Count.ShouldBe(2);
        roundTripped.UserGroups.ShouldContain(userGroup1);
        roundTripped.UserGroups.ShouldContain(userGroup2);
        roundTripped.AllowedConnections.Count.ShouldBe(1);
        roundTripped.AllowedConnections.ShouldContain(connection1);
        roundTripped.Version.ShouldBe(3);
        roundTripped.DateCreated.ShouldBe(workspace.DateCreated);
        roundTripped.DateModified.ShouldBe(workspace.DateModified);
        roundTripped.CreatedByUserId.ShouldBe(workspace.CreatedByUserId);
        roundTripped.ModifiedByUserId.ShouldBe(workspace.ModifiedByUserId);
    }

    [Fact]
    public void BuildDomain_EmptyCollections_ReturnsEmptyLists()
    {
        var entity = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            Alias = "empty",
            Name = "Empty",
            ServiceAccountKey = Guid.NewGuid(),
            Version = 1,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
            UserGroups = [],
            AllowedConnections = [],
        };

        var workspace = WorkspaceFactory.BuildDomain(entity);

        workspace.UserGroups.ShouldBeEmpty();
        workspace.AllowedConnections.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateEntity_DoesNotModifyCreatedFields()
    {
        var originalCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalCreatedBy = Guid.NewGuid();

        var entity = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            Alias = "original",
            Name = "Original",
            ServiceAccountKey = Guid.NewGuid(),
            Version = 1,
            DateCreated = originalCreated,
            DateModified = originalCreated,
            CreatedByUserId = originalCreatedBy,
            UserGroups = [],
            AllowedConnections = [],
        };

        var updated = new Workspace
        {
            Alias = "updated",
            Name = "Updated",
            ServiceAccountKey = Guid.NewGuid(),
            Version = 2,
            DateModified = DateTime.UtcNow,
            ModifiedByUserId = Guid.NewGuid(),
        };

        WorkspaceFactory.UpdateEntity(entity, updated);

        entity.Alias.ShouldBe("updated");
        entity.Name.ShouldBe("Updated");
        entity.DateCreated.ShouldBe(originalCreated);
        entity.CreatedByUserId.ShouldBe(originalCreatedBy);
    }

    [Fact]
    public void BuildEntity_SetsWorkspaceIdOnJoinEntities()
    {
        var workspaceId = Guid.NewGuid();
        var userGroupId = Guid.NewGuid();
        var connectionId = Guid.NewGuid();

        var workspace = new Workspace
        {
            Id = workspaceId,
            Alias = "test",
            Name = "Test",
            ServiceAccountKey = Guid.NewGuid(),
            UserGroups = [userGroupId],
            AllowedConnections = [connectionId],
        };

        var entity = WorkspaceFactory.BuildEntity(workspace);

        entity.UserGroups.ShouldHaveSingleItem();
        entity.UserGroups.First().WorkspaceId.ShouldBe(workspaceId);
        entity.UserGroups.First().UserGroupId.ShouldBe(userGroupId);

        entity.AllowedConnections.ShouldHaveSingleItem();
        entity.AllowedConnections.First().WorkspaceId.ShouldBe(workspaceId);
        entity.AllowedConnections.First().ConnectionId.ShouldBe(connectionId);
    }
}
