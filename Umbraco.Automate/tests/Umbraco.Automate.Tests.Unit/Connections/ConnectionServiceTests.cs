using System.Data;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Versioning;
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

        // Default GetByIdsAsync to return empty — tests for batch fetch can override.
        _repo.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Connection>());

        _service = new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => []),
            Mock.Of<IEntityVersionService>(),
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

    [Fact]
    public async Task TestConnectionAsync_ReturnsNull_WhenConnectionNotFound()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var result = await _service.TestConnectionAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFailure_WhenTypeNotRegistered()
    {
        // Provider package uninstalled / stale connection row scenario.
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "a", Name = "N", Type = "unknown" });

        var result = await _service.TestConnectionAsync(id);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Message.ShouldNotBeNull().ShouldContain("unknown");
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsFailure_WhenValidateThrows()
    {
        // Any unhandled exception from the type's ValidateAsync gets wrapped so the UI
        // always sees a structured failure rather than a 500.
        var type = new ThrowingConnectionType();
        var service = new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => [type]),
            Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());

        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "a", Name = "N", Type = "throwing" });

        var result = await service.TestConnectionAsync(id);

        result.ShouldNotBeNull();
        result.Status.ShouldBe(ConnectionValidationStatus.Failure);
        result.Details.ShouldContain("boom");
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsResult_FromConnectionType()
    {
        var expected = ConnectionValidationResult.Success("ok");
        var type = new StubConnectionType(expected);
        var service = new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => [type]),
            Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());

        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "a", Name = "N", Type = "stub" });

        var result = await service.TestConnectionAsync(id);

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task TestConnectionAsync_RethrowsCancellation()
    {
        // Cancellation must propagate — wrapping it as a "Failure" would hide genuine
        // client-disconnect behaviour and break cooperative cancellation for callers.
        var type = new StubConnectionType(validateImpl: (_, ct) => throw new OperationCanceledException(ct));
        var service = new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => [type]),
            Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());

        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "a", Name = "N", Type = "stub" });

        await Should.ThrowAsync<OperationCanceledException>(
            () => service.TestConnectionAsync(id, new CancellationToken(canceled: true)));
    }

    [ConnectionType("throwing", "Throwing")]
    private sealed class ThrowingConnectionType : ConnectionTypeBase<object>
    {
        public ThrowingConnectionType()
            : base(new ConnectionTypeInfrastructure(Mock.Of<Umbraco.Automate.Core.Settings.IEditableModelResolver>()))
        {
        }

        public override Task<ConnectionValidationResult> ValidateAsync(object? settings, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    [ConnectionType("stub", "Stub")]
    private sealed class StubConnectionType : ConnectionTypeBase<object>
    {
        private readonly Func<object?, CancellationToken, Task<ConnectionValidationResult>> _validate;

        public StubConnectionType(ConnectionValidationResult result)
            : this((_, _) => Task.FromResult(result))
        {
        }

        public StubConnectionType(Func<object?, CancellationToken, Task<ConnectionValidationResult>> validateImpl)
            : base(new ConnectionTypeInfrastructure(Mock.Of<Umbraco.Automate.Core.Settings.IEditableModelResolver>()))
        {
            _validate = validateImpl;
        }

        public override Task<ConnectionValidationResult> ValidateAsync(object? settings, CancellationToken cancellationToken)
            => _validate(settings, cancellationToken);
    }
}
