using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace Umbraco.Automate.Core.Notifications.Channels.BuiltIn;

/// <summary>
/// Notification channel that sends an email via the CMS <see cref="IEmailSender"/>.
/// </summary>
[NotificationChannel("umbracoAutomate.email", "Email",
    Description = "Sends an email notification.",
    Icon = "icon-message")]
public sealed class EmailNotificationChannel : NotificationChannelBase<EmailNotificationChannelSettings>
{
    private const string EmailType = "AutomateNotification";

    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailNotificationChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailNotificationChannel"/> class.
    /// </summary>
    public EmailNotificationChannel(
        NotificationChannelInfrastructure infrastructure,
        IEmailSender emailSender,
        ILogger<EmailNotificationChannel> logger)
        : base(infrastructure)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task NotifyAsync(
        NotificationMessage message,
        EmailNotificationChannelSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Recipients))
        {
            _logger.LogWarning("Email notification channel has no recipients configured, skipping");
            return;
        }

        if (!_emailSender.CanSendRequiredEmail())
        {
            _logger.LogWarning("Email sender is not configured — cannot send notification");
            return;
        }

        var subject = message.Subject ?? string.Empty;
        var body = message.HtmlBody ?? message.TextBody ?? string.Empty;
        var isHtml = message.HtmlBody is not null;

        var recipients = settings.Recipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0)
        {
            return;
        }

        var emailMessage = new EmailMessage(
            from: null, // Uses the CMS global "from" address
            to: recipients,
            cc: null,
            bcc: null,
            replyTo: null,
            subject: subject,
            body: body,
            isBodyHtml: isHtml,
            attachments: null);

        await _emailSender.SendAsync(emailMessage, EmailType, enableNotification: false, expires: null);
    }
}
