using System.Data;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Tests.Common.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class AutomationServiceTests
{
    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly AutomationService _service;

    public AutomationServiceTests()
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

        _service = new AutomationService(
            _repo.Object,
            _runRepo.Object,
            Mock.Of<IEntityVersionService>(),
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>());
    }

    [Fact]
    public async Task PublishAutomationAsync_SetsPublishedVersionAndStatus()
    {
        var id = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(id)
            .AsDraft()
            .WithVersion(3)
            .Build();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.PublishAutomationAsync(id);

        result.PublishedVersion.ShouldBe(3);
        result.Status.ShouldBe(AutomationStatus.Published);
        result.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task UnpublishAutomationAsync_SetsInactiveAndDisabled()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id);

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.UnpublishAutomationAsync(id);

        result.Status.ShouldBe(AutomationStatus.Inactive);
        result.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAutomationAsync_NotFound_Throws()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.PublishAutomationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UnpublishAutomationAsync_NotFound_Throws()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _service.UnpublishAutomationAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAutomationAsync_AssignsIdWhenEmpty()
    {
        Automation automation = new AutomationBuilder().AsDraft();

        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.CreateAutomationAsync(automation);

        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteAutomationAsync_NotFound_ReturnsFalse()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _service.DeleteAutomationAsync(Guid.NewGuid());

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAutomationAsync_Found_DeletesRunsAndAutomation()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id);

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.DeleteAutomationAsync(id);

        result.ShouldBeTrue();
        _runRepo.Verify(r => r.DeleteByAutomationAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAutomationAsync_CancelledByNotification_Throws()
    {
        var id = Guid.NewGuid();
        Automation automation = new AutomationBuilder().WithId(id).AsDraft();

        _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        // Simulate notification cancellation
        _notifications.Setup(n => n.PublishCancelableAsync(It.IsAny<AutomationPublishingNotification>()))
            .ReturnsAsync(true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => _service.PublishAutomationAsync(id));
    }
}
