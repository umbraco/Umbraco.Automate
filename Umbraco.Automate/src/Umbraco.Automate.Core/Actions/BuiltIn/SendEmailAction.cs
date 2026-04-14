using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that sends an email via the CMS <see cref="IEmailSender"/>.
/// </summary>
[Action("umbracoAutomate.sendEmail", "Send Email",
    Description = "Sends an email to one or more recipients.",
    Group = "Core",
    Icon = "icon-message")]
public sealed class SendEmailAction : ActionBase<SendEmailSettings, SendEmailOutput>
{
    private const string EmailType = "AutomateAction";

    private readonly IEmailSender _emailSender;
    private readonly ILogger<SendEmailAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendEmailAction"/> class.
    /// </summary>
    public SendEmailAction(
        ActionInfrastructure infrastructure,
        IEmailSender emailSender,
        ILogger<SendEmailAction> logger)
        : base(infrastructure)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SendEmailSettings>();

        if (string.IsNullOrWhiteSpace(settings.To))
        {
            return ActionResult.Failed(
                new ArgumentException("At least one recipient is required."),
                StepRunErrorCategory.Validation);
        }

        if (!_emailSender.CanSendRequiredEmail())
        {
            return ActionResult.Failed(
                new InvalidOperationException("Email sender is not configured. Check SMTP settings."),
                StepRunErrorCategory.ConfigurationError);
        }

        var recipients = settings.To
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0)
        {
            return ActionResult.Failed(
                new ArgumentException("At least one valid recipient is required."),
                StepRunErrorCategory.Validation);
        }

        var emailMessage = new EmailMessage(
            from: null,
            to: recipients,
            cc: null,
            bcc: null,
            replyTo: null,
            subject: settings.Subject,
            body: settings.Body,
            isBodyHtml: settings.IsHtml,
            attachments: null);

        await _emailSender.SendAsync(emailMessage, EmailType, enableNotification: false, expires: null);

        _logger.LogInformation(
            "Automation {AutomationId} / Run {RunId}: Sent email to {RecipientCount} recipient(s)",
            context.AutomationId, context.RunId, recipients.Length);

        return Success(new SendEmailOutput
        {
            RecipientCount = recipients.Length,
            Subject = settings.Subject,
        });
    }
}
