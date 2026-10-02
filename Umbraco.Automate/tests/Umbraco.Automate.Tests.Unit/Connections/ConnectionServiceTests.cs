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
            Mock.Of<IEventMessagesFactory>(),
            []);
    }

    private ConnectionService CreateServiceWithHandler(
        IConnectionSettingsSaveHandler handler,
        IEntityVersionService? versionService = null)
    {
        var connectionType = new Mock<IConnectionType>();
        connectionType.Setup(t => t.Alias).Returns("slack");

        return new ConnectionService(
            _repo.Object,
            new ConnectionTypeCollection(() => [connectionType.Object]),
            versionService ?? Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            [handler]);
    }

    [Fact]
    public async Task CreateConnectionAsync_RunsSettingsSaveHandlers_AndPersistsTheirChanges()
    {
        ConnectionSettingsSaveContext? captured = null;
        var handler = new Mock<IConnectionSettingsSaveHandler>();
        handler.Setup(h => h.PrepareSettingsAsync(It.IsAny<ConnectionSettingsSaveContext>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectionSettingsSaveContext, CancellationToken>((c, _) =>
            {
                captured = c;
                c.Connection.Settings["value"] = "rewritten";
            })
            .Returns(Task.CompletedTask);

        Connection? saved = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Connection, Guid?, CancellationToken>((c, _, _) => saved = c)
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        var service = CreateServiceWithHandler(handler.Object);
        await service.CreateConnectionAsync(new Connection
        {
            Alias = "test",
            Name = "Test",
            Type = "slack",
            Settings = new() { ["value"] = "submitted" },
        });

        captured.ShouldNotBeNull();
        captured.PersistedSettings.ShouldBeNull();
        captured.IsRollback.ShouldBeFalse();
        captured.Connection.Id.ShouldNotBe(Guid.Empty);
        saved.ShouldNotBeNull();
        saved.Settings["value"].ShouldBe("rewritten");
    }

    [Fact]
    public async Task UpdateConnectionAsync_PassesFreshlyLoadedPersistedSettings_ToHandlers()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "test", Name = "Test", Type = "slack", Settings = new() { ["value"] = "stored" } });
        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        ConnectionSettingsSaveContext? captured = null;
        var handler = new Mock<IConnectionSettingsSaveHandler>();
        handler.Setup(h => h.PrepareSettingsAsync(It.IsAny<ConnectionSettingsSaveContext>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectionSettingsSaveContext, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        var service = CreateServiceWithHandler(handler.Object);
        await service.UpdateConnectionAsync(new Connection
        {
            Id = id,
            Alias = "test",
            Name = "Test",
            Type = "slack",
            Settings = new() { ["value"] = "submitted" },
        });

        captured.ShouldNotBeNull();
        captured.PersistedSettings.ShouldNotBeNull();
        captured.PersistedSettings!["value"].ShouldBe("stored");
        captured.IsRollback.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateConnectionAsync_DoesNotSave_WhenHandlerRejectsSettings()
    {
        var handler = new Mock<IConnectionSettingsSaveHandler>();
        handler.Setup(h => h.PrepareSettingsAsync(It.IsAny<ConnectionSettingsSaveContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConnectionSettingsValidationException("nope"));

        var service = CreateServiceWithHandler(handler.Object);

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() => service.UpdateConnectionAsync(
            new Connection { Id = Guid.NewGuid(), Alias = "test", Name = "Test", Type = "slack" }));

        _repo.Verify(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RollbackConnectionAsync_FlagsRollback_ToHandlers()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Connection { Id = id, Alias = "test", Name = "Test", Type = "slack" });
        _repo.Setup(r => r.SaveAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        var versions = new Mock<IEntityVersionService>();
        versions.Setup(v => v.GetVersionSnapshotAsync<Connection>(id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Connection { Id = id, Alias = "test", Name = "Test", Type = "slack" });

        ConnectionSettingsSaveContext? captured = null;
        var handler = new Mock<IConnectionSettingsSaveHandler>();
        handler.Setup(h => h.PrepareSettingsAsync(It.IsAny<ConnectionSettingsSaveContext>(), It.IsAny<CancellationToken>()))
            .Callback<ConnectionSettingsSaveContext, CancellationToken>((c, _) => captured = c)
            .Returns(Task.CompletedTask);

        var service = CreateServiceWithHandler(handler.Object, versions.Object);
        await service.RollbackConnectionAsync(id, 1);

        captured.ShouldNotBeNull();
        captured.IsRollback.ShouldBeTrue();
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
            Mock.Of<IEventMessagesFactory>(),
            []);

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
            Mock.Of<IEventMessagesFactory>(),
            []);

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
            Mock.Of<IEventMessagesFactory>(),
            []);

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
