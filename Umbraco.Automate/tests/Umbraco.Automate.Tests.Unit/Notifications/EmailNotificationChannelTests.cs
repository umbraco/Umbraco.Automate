using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Notifications.Channels.BuiltIn;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace Umbraco.Automate.Tests.Unit.Notifications;

public class EmailNotificationChannelTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly EmailNotificationChannel _channel;

    public EmailNotificationChannelTests()
    {
        _emailSender.Setup(e => e.CanSendRequiredEmail()).Returns(true);

        var infrastructure = new NotificationChannelInfrastructure(Mock.Of<IEditableModelResolver>());
        _channel = new EmailNotificationChannel(
            infrastructure,
            _emailSender.Object,
            Mock.Of<ILogger<EmailNotificationChannel>>());
    }

    private static RunFailureNotification CreateNotification() => new()
    {
        AutomationId = Guid.NewGuid(),
        AutomationName = "Deploy to Production",
        RunId = Guid.NewGuid(),
        Status = AutomationRunStatus.Failed,
        Error = "Connection timed out",
        WorkspaceId = Guid.NewGuid(),
        CompletedUtc = DateTime.UtcNow,
    };

    [Fact]
    public void Alias_Is_Correct()
    {
        _channel.Alias.ShouldBe("umbracoAutomate.email");
    }

    [Fact]
    public async Task NotifyAsync_SendsEmail_ToRecipients()
    {
        var notification = CreateNotification();
        var settings = new EmailNotificationChannelSettings
        {
            Recipients = "admin@example.com, ops@example.com",
        };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(notification, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.To.Length.ShouldBe(2);
        sentMessage.To[0].ShouldBe("admin@example.com");
        sentMessage.To[1].ShouldBe("ops@example.com");
        sentMessage.IsBodyHtml.ShouldBeTrue();
        sentMessage.Subject.ShouldContain("Deploy to Production");
        sentMessage.Subject.ShouldContain("Failed");
    }

    [Fact]
    public async Task NotifyAsync_CustomSubjectTemplate_AppliesPlaceholders()
    {
        var notification = CreateNotification();
        var settings = new EmailNotificationChannelSettings
        {
            Recipients = "admin@example.com",
            SubjectTemplate = "ALERT: ${automationName} is ${status}",
        };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(notification, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.Subject.ShouldBe("ALERT: Deploy to Production is Failed");
    }

    [Fact]
    public async Task NotifyAsync_NoRecipients_DoesNotSend()
    {
        var notification = CreateNotification();
        var settings = new EmailNotificationChannelSettings { Recipients = "" };

        await ((INotificationChannel)_channel).NotifyAsync(notification, settings, CancellationToken.None);

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_EmailSenderNotConfigured_DoesNotSend()
    {
        _emailSender.Setup(e => e.CanSendRequiredEmail()).Returns(false);

        var notification = CreateNotification();
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        await ((INotificationChannel)_channel).NotifyAsync(notification, settings, CancellationToken.None);

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_BodyContainsErrorDetails()
    {
        var notification = CreateNotification();
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(notification, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.Body.ShouldNotBeNull();
        sentMessage.Body.ShouldContain("Connection timed out");
        sentMessage.Body.ShouldContain("Deploy to Production");
    }
}
