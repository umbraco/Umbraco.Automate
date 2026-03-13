using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace Umbraco.Automate.Core.Notifications.Channels.BuiltIn;

/// <summary>
/// Notification channel that sends an email via the CMS <see cref="IEmailSender"/> when a run fails or suspends.
/// </summary>
[NotificationChannel("umbracoAutomate.email", "Email")]
public sealed class EmailNotificationChannel : NotificationChannelBase<EmailNotificationChannelSettings>
{
    private const string DefaultSubjectTemplate = "[Umbraco Automate] ${automationName} — ${status}";
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
    public override string? Description => "Sends an email notification when a run fails or suspends.";

    /// <inheritdoc />
    public override string? Icon => "icon-message";

    /// <inheritdoc />
    protected override async Task NotifyAsync(
        RunFailureNotification notification,
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
            _logger.LogWarning("Email sender is not configured — cannot send automation failure notification for run {RunId}", notification.RunId);
            return;
        }

        var subject = BuildSubject(settings.SubjectTemplate, notification);
        var body = BuildBody(notification);

        var recipients = settings.Recipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0)
        {
            return;
        }

        var message = new EmailMessage(
            from: null, // Uses the CMS global "from" address
            to: recipients,
            cc: null,
            bcc: null,
            replyTo: null,
            subject: subject,
            body: body,
            isBodyHtml: true,
            attachments: null);

        await _emailSender.SendAsync(message, EmailType, enableNotification: false, expires: null);
    }

    private static string BuildSubject(string? template, RunFailureNotification notification)
    {
        var subject = string.IsNullOrWhiteSpace(template) ? DefaultSubjectTemplate : template;

        return subject
            .Replace("${automationName}", notification.AutomationName, StringComparison.OrdinalIgnoreCase)
            .Replace("${status}", notification.Status.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBody(RunFailureNotification notification)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Automation Run ");
        sb.Append(notification.Status);
        sb.Append("</h2>");
        sb.Append("<table style=\"border-collapse:collapse;\">");
        AppendRow(sb, "Automation", notification.AutomationName);
        AppendRow(sb, "Run ID", notification.RunId.ToString());
        AppendRow(sb, "Status", notification.Status.ToString());

        if (!string.IsNullOrWhiteSpace(notification.Error))
        {
            AppendRow(sb, "Error", notification.Error);
        }

        if (notification.CompletedUtc.HasValue)
        {
            AppendRow(sb, "Completed (UTC)", notification.CompletedUtc.Value.ToString("u"));
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, string value)
    {
        sb.Append("<tr><td style=\"padding:4px 12px 4px 0;font-weight:bold;vertical-align:top;\">");
        sb.Append(System.Net.WebUtility.HtmlEncode(label));
        sb.Append("</td><td style=\"padding:4px 0;\">");
        sb.Append(System.Net.WebUtility.HtmlEncode(value));
        sb.Append("</td></tr>");
    }
}
