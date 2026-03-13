using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Tests.Unit.Notifications;

public class RunCompletedNotificationDispatcherTests
{
    private readonly Mock<INotificationChannel> _channel = new();
    private readonly Mock<NotificationChannelCollection> _channelCollection;
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly RunCompletedNotificationDispatcher _dispatcher;

    public RunCompletedNotificationDispatcherTests()
    {
        _channelCollection = new Mock<NotificationChannelCollection>(
            (Func<IEnumerable<INotificationChannel>>)(() => [_channel.Object]));

        _channel.Setup(c => c.Alias).Returns("umbracoAutomate.webhook");

        _dispatcher = new RunCompletedNotificationDispatcher(
            _channelCollection.Object,
            _automationService.Object,
            Mock.Of<ILogger<RunCompletedNotificationDispatcher>>());
    }

    private static AutomationRun CreateRun(
        AutomationRunStatus status = AutomationRunStatus.Failed,
        Guid? automationId = null) => new()
    {
        AutomationId = automationId ?? Guid.NewGuid(),
        AutomationVersion = 1,
        WorkspaceId = Guid.NewGuid(),
        ServiceAccountKey = Guid.NewGuid(),
        InitiatedBy = "system",
        Status = status,
        Error = status == AutomationRunStatus.Failed ? "Something went wrong" : null,
        CompletedUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task HandleAsync_SuccessfulRun_DoesNotNotify()
    {
        var run = CreateRun(AutomationRunStatus.Completed);
        var notification = new AutomationRunCompletedNotification(run, new EventMessages());

        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<RunFailureNotification>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_FailedRun_WithChannelConfigured_Notifies()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);

        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test Automation",
            NotificationSettings = new AutomationNotificationSettings
            {
                NotifyOn = NotifyOn.Failed,
                Channels =
                [
                    new ChannelConfiguration
                    {
                        ChannelAlias = "umbracoAutomate.webhook",
                        IsEnabled = true,
                        Settings = new Dictionary<string, object?> { ["Url"] = "https://example.com/hook" },
                    }
                ],
            },
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _channelCollection.Setup(c => c.GetByAlias("umbracoAutomate.webhook"))
            .Returns(_channel.Object);

        _channel.Setup(c => c.ResolveSettings(It.IsAny<Dictionary<string, object?>>()))
            .Returns(new object());

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(
                It.Is<RunFailureNotification>(n => n.RunId == run.Id && n.AutomationName == "Test Automation"),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_FailedRun_NoNotificationSettings_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);

        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test",
            NotificationSettings = null,
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<RunFailureNotification>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SuspendedRun_NotifyOnFailed_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Suspended, automationId);

        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test",
            NotificationSettings = new AutomationNotificationSettings
            {
                NotifyOn = NotifyOn.Failed,
                Channels =
                [
                    new ChannelConfiguration { ChannelAlias = "umbracoAutomate.webhook", IsEnabled = true }
                ],
            },
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<RunFailureNotification>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelThrows_DoesNotThrow()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);

        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test",
            NotificationSettings = new AutomationNotificationSettings
            {
                NotifyOn = NotifyOn.Failed,
                Channels =
                [
                    new ChannelConfiguration { ChannelAlias = "umbracoAutomate.webhook", IsEnabled = true }
                ],
            },
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _channelCollection.Setup(c => c.GetByAlias("umbracoAutomate.webhook"))
            .Returns(_channel.Object);

        _channel.Setup(c => c.NotifyAsync(It.IsAny<RunFailureNotification>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());

        // Should not throw
        await _dispatcher.HandleAsync(notification, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_DisabledChannel_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);

        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test",
            NotificationSettings = new AutomationNotificationSettings
            {
                NotifyOn = NotifyOn.Failed,
                Channels =
                [
                    new ChannelConfiguration { ChannelAlias = "umbracoAutomate.webhook", IsEnabled = false }
                ],
            },
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<RunFailureNotification>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
