using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Notifications.Channels.BuiltIn;
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

    private static NotificationMessage CreateMessage() => new()
    {
        Subject = "[Umbraco Automate] Deploy to Production \u2014 Failed",
        HtmlBody = "<h2>Automation Run Failed</h2><table><tr><td>Error</td><td>Connection timed out</td></tr></table>",
    };

    [Fact]
    public void Alias_Is_Correct()
    {
        _channel.Alias.ShouldBe("umbracoAutomate.email");
    }

    [Fact]
    public async Task NotifyAsync_SendsEmail_ToRecipients()
    {
        var message = CreateMessage();
        var settings = new EmailNotificationChannelSettings
        {
            Recipients = "admin@example.com, ops@example.com",
        };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.To.Length.ShouldBe(2);
        sentMessage.To[0].ShouldBe("admin@example.com");
        sentMessage.To[1].ShouldBe("ops@example.com");
        sentMessage.IsBodyHtml.ShouldBeTrue();
        sentMessage.Subject.ShouldContain("Deploy to Production");
        sentMessage.Subject.ShouldContain("Failed");
    }

    [Fact]
    public async Task NotifyAsync_UsesSubjectFromMessage()
    {
        var message = new NotificationMessage
        {
            Subject = "Custom subject line",
            HtmlBody = "<p>Body</p>",
        };
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.Subject.ShouldBe("Custom subject line");
    }

    [Fact]
    public async Task NotifyAsync_FallsBackToTextBody_WhenNoHtmlBody()
    {
        var message = new NotificationMessage
        {
            Subject = "Subject",
            TextBody = "Plain text body",
        };
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.Body.ShouldBe("Plain text body");
        sentMessage.IsBodyHtml.ShouldBeFalse();
    }

    [Fact]
    public async Task NotifyAsync_NoRecipients_DoesNotSend()
    {
        var message = CreateMessage();
        var settings = new EmailNotificationChannelSettings { Recipients = "" };

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_EmailSenderNotConfigured_DoesNotSend()
    {
        _emailSender.Setup(e => e.CanSendRequiredEmail()).Returns(false);

        var message = CreateMessage();
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        _emailSender.Verify(
            e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()),
            Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_BodyContainsErrorDetails()
    {
        var message = CreateMessage();
        var settings = new EmailNotificationChannelSettings { Recipients = "admin@example.com" };

        EmailMessage? sentMessage = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((msg, _, _, _) => sentMessage = msg)
            .Returns(Task.CompletedTask);

        await ((INotificationChannel)_channel).NotifyAsync(message, settings, CancellationToken.None);

        sentMessage.ShouldNotBeNull();
        sentMessage.Body.ShouldNotBeNull();
        sentMessage.Body.ShouldContain("Connection timed out");
    }
}
