using System.Data;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Connections;

public class ConnectionServiceTests
{
    private readonly Mock<IConnectionRepository> _repo = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly ConnectionService _service;

    public ConnectionServiceTests()
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

        _service = new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => []),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());
    }

    [Fact]
    public async Task CreateConnectionAsync_AssignsIdWhenEmpty()
    {
        var connection = new Connection { Alias = "test", Name = "Test", Type = "slack" };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        var result = await _service.CreateConnectionAsync(connection);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateConnectionAsync_PreservesExistingId()
    {
        var id = Guid.NewGuid();
        var connection = new Connection { Id = id, Alias = "test", Name = "Test", Type = "slack" };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        var result = await _service.CreateConnectionAsync(connection);

        result.Id.ShouldBe(id);
    }

    [Fact]
    public async Task DeleteConnectionAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var result = await _service.DeleteConnectionAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteConnectionAsync_Found_DeletesAndPublishesNotification()
    {
        var id = Guid.NewGuid();
        var connection = new Connection { Id = id, Alias = "test", Name = "Test", Type = "slack" };

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteConnectionAsync(id);

        result.ShouldBeTrue();
        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateConnectionAsync_CancelledByNotification_Throws()
    {
        var connection = new Connection { Alias = "test", Name = "Test", Type = "slack" };

        _notifications.Setup(n => n.PublishCancelable(It.IsAny<ConnectionSavingNotification>()))
            .Returns(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => _service.CreateConnectionAsync(connection));
    }

    [Fact]
    public async Task UpdateConnectionAsync_DelegatesToRepository()
    {
        var connection = new Connection { Id = Guid.NewGuid(), Alias = "test", Name = "Test", Type = "slack" };

        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        var result = await _service.UpdateConnectionAsync(connection);

        result.Alias.ShouldBe("test");
        _repo.Verify(r => r.SaveAsync(connection, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
