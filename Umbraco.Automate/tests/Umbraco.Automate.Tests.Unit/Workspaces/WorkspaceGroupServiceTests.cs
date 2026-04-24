using System.Data;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Workspaces;

public class WorkspaceGroupServiceTests
{
    private readonly Mock<IWorkspaceGroupRepository> _groupRepo = new();
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationRepository> _automationRepo = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly WorkspaceGroupService _service;

    public WorkspaceGroupServiceTests()
    {
        _scope.Setup(s => s.Notifications).Returns(_notifications.Object);
        _scopeProvider.Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_scope.Object);

        // Default workspace mock — returns a valid workspace for any ID.
        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceBuilder().Build());

        _service = new WorkspaceGroupService(
            _groupRepo.Object,
            _automationService.Object,
            _automationRepo.Object,
            _workspaceService.Object,
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());
    }

    [Fact]
    public async Task CreateGroupAsync_Success()
    {
        var workspaceId = Guid.NewGuid();
        var group = new WorkspaceGroup { Name = "My Group", WorkspaceId = workspaceId };

        _groupRepo.Setup(r => r.NameExistsAsync(workspaceId, null, "My Group", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _groupRepo.Setup(r => r.SaveAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        var result = await _service.CreateGroupAsync(group);

        result.Name.ShouldBe("My Group");
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateGroupAsync_WorkspaceNotFound_Throws()
    {
        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var group = new WorkspaceGroup { Name = "Test", WorkspaceId = Guid.NewGuid() };

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(group));

        ex.Message.ShouldContain("Workspace");
    }

    [Fact]
    public async Task CreateGroupAsync_ParentNotFound_Throws()
    {
        var parentId = Guid.NewGuid();
        var group = new WorkspaceGroup { Name = "Test", WorkspaceId = Guid.NewGuid(), ParentId = parentId };

        _groupRepo.Setup(r => r.GetAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup?)null);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(group));

        ex.Message.ShouldContain("Parent group");
    }

    [Fact]
    public async Task CreateGroupAsync_DuplicateName_Throws()
    {
        var workspaceId = Guid.NewGuid();
        var group = new WorkspaceGroup { Name = "Existing", WorkspaceId = workspaceId };

        _groupRepo.Setup(r => r.NameExistsAsync(workspaceId, null, "Existing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(group));

        ex.Message.ShouldContain("already exists");
    }

    [Fact]
    public async Task CreateGroupAsync_ParentInDifferentWorkspace_Throws()
    {
        var workspaceId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var group = new WorkspaceGroup { Name = "Test", WorkspaceId = workspaceId, ParentId = parentId };

        var parentInOtherWorkspace = new WorkspaceGroup { Id = parentId, Name = "Parent", WorkspaceId = Guid.NewGuid() };
        _groupRepo.Setup(r => r.GetAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentInOtherWorkspace);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _service.CreateGroupAsync(group));

        ex.Message.ShouldContain("different workspace");
    }

    [Fact]
    public async Task UpdateGroupAsync_RenameSuccess()
    {
        var groupId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var existing = new WorkspaceGroup { Id = groupId, Name = "Old Name", WorkspaceId = workspaceId };

        _groupRepo.Setup(r => r.GetAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _groupRepo.Setup(r => r.NameExistsAsync(workspaceId, null, "New Name", groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _groupRepo.Setup(r => r.SaveAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        var update = new WorkspaceGroup { Id = groupId, Name = "New Name" };
        var result = await _service.UpdateGroupAsync(update);

        result.Name.ShouldBe("New Name");
    }

    [Fact]
    public async Task UpdateGroupAsync_NotFound_Throws()
    {
        _groupRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup?)null);

        var update = new WorkspaceGroup { Id = Guid.NewGuid(), Name = "Test" };

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UpdateGroupAsync(update));
    }

    [Fact]
    public async Task DeleteGroupAsync_EmptyGroup_Success()
    {
        var groupId = Guid.NewGuid();
        var group = new WorkspaceGroup { Id = groupId, Name = "Test", WorkspaceId = Guid.NewGuid() };

        _groupRepo.Setup(r => r.GetAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _groupRepo.Setup(r => r.GetChildIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Guid>());
        _automationRepo.Setup(r => r.GetPagedAsync(null, null, groupId, 0, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Automation>(), 0));
        _groupRepo.Setup(r => r.DeleteAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteGroupAsync(groupId);

        result.ShouldBeTrue();
        _groupRepo.Verify(r => r.DeleteAsync(groupId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteGroupAsync_CascadeDeletesAutomations()
    {
        var groupId = Guid.NewGuid();
        var automationId = Guid.NewGuid();
        var group = new WorkspaceGroup { Id = groupId, Name = "Test", WorkspaceId = Guid.NewGuid() };
        var automation = new AutomationBuilder().WithId(automationId).Build();

        _groupRepo.Setup(r => r.GetAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        _groupRepo.Setup(r => r.GetChildIdsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Guid>());
        _automationRepo.Setup(r => r.GetPagedAsync(null, null, groupId, 0, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { automation }.AsEnumerable(), 1));
        _groupRepo.Setup(r => r.DeleteAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.DeleteGroupAsync(groupId);

        _automationService.Verify(s => s.DeleteAutomationAsync(automationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteGroupAsync_CascadeDeletesChildGroupsRecursively()
    {
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var parent = new WorkspaceGroup { Id = parentId, Name = "Parent", WorkspaceId = workspaceId };
        var child = new WorkspaceGroup { Id = childId, Name = "Child", WorkspaceId = workspaceId, ParentId = parentId };

        _groupRepo.Setup(r => r.GetAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _groupRepo.Setup(r => r.GetAsync(childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(child);
        _groupRepo.Setup(r => r.GetChildIdsAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { childId });
        _groupRepo.Setup(r => r.GetChildIdsAsync(childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Guid>());

        _automationRepo.Setup(r => r.GetPagedAsync(null, null, It.IsAny<Guid>(), 0, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Automation>(), 0));

        _groupRepo.Setup(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _service.DeleteGroupAsync(parentId);

        _groupRepo.Verify(r => r.DeleteAsync(childId, It.IsAny<CancellationToken>()), Times.Once);
        _groupRepo.Verify(r => r.DeleteAsync(parentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveGroupForDeployAsync_Upserts_BypassingInteractiveValidations()
    {
        // Deploy orders artifacts via its own dependency graph and may land a nested
        // group at the root temporarily. The interactive create validators (workspace
        // existence, parent existence, unique name) would fail in that window, so the
        // Deploy-facing save must skip them while still going through the repository
        // and firing notifications.
        var workspaceId = Guid.NewGuid();
        var group = new WorkspaceGroup
        {
            Id = Guid.NewGuid(),
            Name = "Campaigns",
            WorkspaceId = workspaceId,
            ParentId = null,
        };

        _workspaceService.Setup(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);
        _groupRepo.Setup(r => r.NameExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _groupRepo.Setup(r => r.SaveAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        var result = await _service.SaveGroupForDeployAsync(group);

        result.Id.ShouldBe(group.Id);
        _groupRepo.Verify(r => r.SaveAsync(group, It.IsAny<CancellationToken>()), Times.Once);
        _workspaceService.Verify(w => w.GetWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _groupRepo.Verify(r => r.NameExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveGroupForDeployAsync_AssignsNewId_WhenEmpty()
    {
        var group = new WorkspaceGroup { Name = "Campaigns", WorkspaceId = Guid.NewGuid() };

        _groupRepo.Setup(r => r.SaveAsync(It.IsAny<WorkspaceGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup g, CancellationToken _) => g);

        var result = await _service.SaveGroupForDeployAsync(group);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteGroupAsync_NotFound_ReturnsFalse()
    {
        _groupRepo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceGroup?)null);

        var result = await _service.DeleteGroupAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }
}
