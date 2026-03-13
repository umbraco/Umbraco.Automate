using System.Data;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Workspaces;

public class WorkspaceServiceTests
{
    private readonly Mock<IWorkspaceRepository> _repo = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
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

        _service = new WorkspaceService(
            _repo.Object,
            Mock.Of<IAutomationGroupRepository>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());
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
}
