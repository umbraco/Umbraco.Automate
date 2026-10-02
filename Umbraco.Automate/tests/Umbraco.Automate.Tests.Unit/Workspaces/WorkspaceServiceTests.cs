using System.Data;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Workspaces;

public class WorkspaceServiceTests
{
    private readonly Mock<IWorkspaceRepository> _repo = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
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

        // Any service account is an API user unless a test says otherwise.
        _userService.Setup(s => s.GetAsync(It.IsAny<Guid>()))
            .ReturnsAsync(CreateUser(UserKind.Api));

        _service = new WorkspaceService(
            _repo.Object,
            Mock.Of<IWorkspaceGroupRepository>(),
            Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            _userService.Object);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_AssignsIdWhenEmpty()
    {
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace w, Guid? _, CancellationToken _) => w);

        var result = await _service.CreateWorkspaceAsync(workspace);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_PreservesExistingId()
    {
        var id = Guid.NewGuid();
        var workspace = new Workspace { Id = id, Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace w, Guid? _, CancellationToken _) => w);

        var result = await _service.CreateWorkspaceAsync(workspace);

        result.Id.ShouldBe(id);
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var result = await _service.DeleteWorkspaceAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteWorkspaceAsync_Found_DeletesAndPublishesNotification()
    {
        var id = Guid.NewGuid();
        var workspace = new Workspace { Id = id, Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteWorkspaceAsync(id);

        result.ShouldBeTrue();
        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_CancelledByNotification_Throws()
    {
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };

        _notifications.Setup(n => n.PublishCancelable(It.IsAny<WorkspaceSavingNotification>()))
            .Returns(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => _service.CreateWorkspaceAsync(workspace));
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_DelegatesToRepository()
    {
        var workspace = new Workspace { Id = Guid.NewGuid(), Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace w, Guid? _, CancellationToken _) => w);

        var result = await _service.UpdateWorkspaceAsync(workspace);

        result.Alias.ShouldBe("test");
        _repo.Verify(r => r.SaveAsync(workspace, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_RegularUserAsServiceAccount_ThrowsAndDoesNotSave()
    {
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };
        _userService.Setup(s => s.GetAsync(workspace.ServiceAccountKey))
            .ReturnsAsync(CreateUser(UserKind.Default, "Administrator"));

        var exception = await Should.ThrowAsync<WorkspaceServiceAccountValidationException>(
            () => _service.CreateWorkspaceAsync(workspace));

        exception.Message.ShouldContain("Administrator");
        _repo.Verify(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_RegularUserAsServiceAccount_ThrowsAndDoesNotSave()
    {
        // A workspace saved before the rule existed is rejected the next time it is saved.
        var workspace = new Workspace { Id = Guid.NewGuid(), Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };
        _userService.Setup(s => s.GetAsync(workspace.ServiceAccountKey))
            .ReturnsAsync(CreateUser(UserKind.Default));

        await Should.ThrowAsync<WorkspaceServiceAccountValidationException>(
            () => _service.UpdateWorkspaceAsync(workspace));

        _repo.Verify(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_UnknownServiceAccount_Throws()
    {
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };
        _userService.Setup(s => s.GetAsync(workspace.ServiceAccountKey))
            .ReturnsAsync((IUser?)null);

        await Should.ThrowAsync<WorkspaceServiceAccountValidationException>(
            () => _service.CreateWorkspaceAsync(workspace));
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ApiUserAsServiceAccount_Saves()
    {
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.NewGuid() };
        _repo.Setup(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace w, Guid? _, CancellationToken _) => w);

        await _service.CreateWorkspaceAsync(workspace);

        _repo.Verify(r => r.SaveAsync(workspace, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_NoServiceAccount_SavesWithoutLookingUpAUser()
    {
        // A workspace may have no service account; it just can't run anything that needs one.
        var workspace = new Workspace { Alias = "test", Name = "Test", ServiceAccountKey = Guid.Empty };
        _repo.Setup(r => r.SaveAsync(It.IsAny<Workspace>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace w, Guid? _, CancellationToken _) => w);

        await _service.CreateWorkspaceAsync(workspace);

        _repo.Verify(r => r.SaveAsync(workspace, null, It.IsAny<CancellationToken>()), Times.Once);
        _userService.Verify(s => s.GetAsync(It.IsAny<Guid>()), Times.Never);
    }

    private static IUser CreateUser(UserKind kind, string name = "Service account")
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(kind);
        user.SetupGet(u => u.Name).Returns(name);
        return user.Object;
    }
}
