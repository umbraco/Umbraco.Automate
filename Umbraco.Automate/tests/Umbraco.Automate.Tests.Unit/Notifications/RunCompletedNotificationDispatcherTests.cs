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
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly RunCompletedNotificationDispatcher _dispatcher;

    public RunCompletedNotificationDispatcherTests()
    {
        _channel.Setup(c => c.Alias).Returns("umbracoAutomate.webhook");

        var channelCollection = new NotificationChannelCollection(() => [_channel.Object]);

        _dispatcher = new RunCompletedNotificationDispatcher(
            channelCollection,
            _automationService.Object,
            _runService.Object,
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

    private Automation SetupAutomation(Guid automationId, NotifyOn notifyOn, bool channelEnabled = true)
    {
        var automation = new Automation
        {
            Id = automationId,
            Alias = "test",
            Name = "Test Automation",
            NotificationSettings = new AutomationNotificationSettings
            {
                NotifyOn = notifyOn,
                Channels =
                [
                    new ChannelConfiguration
                    {
                        ChannelAlias = "umbracoAutomate.webhook",
                        IsEnabled = channelEnabled,
                        Settings = new Dictionary<string, object?> { ["Url"] = "https://example.com/hook" },
                    }
                ],
            },
        };

        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        _channel.Setup(c => c.ResolveSettings(It.IsAny<Dictionary<string, object?>>()))
            .Returns(new object());

        return automation;
    }

    [Fact]
    public async Task HandleAsync_FailedRun_NotifyOnFailed_Notifies()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);
        SetupAutomation(automationId, NotifyOn.Failed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        NotificationMessage? captured = null;
        _channel.Verify(
            c => c.NotifyAsync(
                It.IsAny<NotificationMessage>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        captured = (NotificationMessage)_channel.Invocations
            .First(i => i.Method.Name == nameof(INotificationChannel.NotifyAsync))
            .Arguments[0];

        captured.Subject.ShouldNotBeNull();
        captured.Subject.ShouldContain("Test Automation");
        captured.Subject.ShouldContain("Failed");
        captured.HtmlBody.ShouldNotBeNull();
        captured.Data.ShouldBeOfType<RunNotification>();
        ((RunNotification)captured.Data!).RunId.ShouldBe(run.Id);
    }

    [Fact]
    public async Task HandleAsync_FailedRun_MessageContainsErrorInHtmlBody()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);
        SetupAutomation(automationId, NotifyOn.Failed);

        NotificationMessage? captured = null;
        _channel
            .Setup(c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationMessage, object?, CancellationToken>((m, _, _) => captured = m)
            .Returns(Task.CompletedTask);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.HtmlBody.ShouldNotBeNull();
        captured.HtmlBody.ShouldContain("Something went wrong");
        captured.HtmlBody.ShouldContain("Test Automation");
    }

    [Fact]
    public async Task HandleAsync_CompletedRun_NotifyOnCompleted_Notifies()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Completed, automationId);
        SetupAutomation(automationId, NotifyOn.Completed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(
                It.IsAny<NotificationMessage>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CompletedRun_NotifyOnFailedOnly_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Completed, automationId);
        SetupAutomation(automationId, NotifyOn.Failed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CompletedRun_Recovered_PreviousWasFailed_Notifies()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Completed, automationId);
        SetupAutomation(automationId, NotifyOn.Recovered);

        _runService.Setup(s => s.GetPreviousTerminalRunStatusAsync(automationId, run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationRunStatus.Failed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(
                It.IsAny<NotificationMessage>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_CompletedRun_Recovered_PreviousWasCompleted_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Completed, automationId);
        SetupAutomation(automationId, NotifyOn.Recovered);

        _runService.Setup(s => s.GetPreviousTerminalRunStatusAsync(automationId, run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationRunStatus.Completed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_CompletedRun_Recovered_NoPreviousRun_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Completed, automationId);
        SetupAutomation(automationId, NotifyOn.Recovered);

        _runService.Setup(s => s.GetPreviousTerminalRunStatusAsync(automationId, run.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AutomationRunStatus?)null);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SuspendedRun_NotifyOnFailed_DoesNotNotify()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Suspended, automationId);
        SetupAutomation(automationId, NotifyOn.Failed);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoNotificationSettings_DoesNotNotify()
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
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ChannelThrows_DoesNotThrow()
    {
        var automationId = Guid.NewGuid();
        var run = CreateRun(AutomationRunStatus.Failed, automationId);
        SetupAutomation(automationId, NotifyOn.Failed);

        _channel.Setup(c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
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
        SetupAutomation(automationId, NotifyOn.Failed, channelEnabled: false);

        var notification = new AutomationRunCompletedNotification(run, new EventMessages());
        await _dispatcher.HandleAsync(notification, CancellationToken.None);

        _channel.Verify(
            c => c.NotifyAsync(It.IsAny<NotificationMessage>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
