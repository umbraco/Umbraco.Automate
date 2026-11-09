using System.Net.Mail;
using Microsoft.Extensions.Logging;
using MimeKit;
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

        var recipients = SplitAddresses(settings.To);

        if (recipients.Length == 0)
        {
            return ActionResult.Failed(
                new ArgumentException("At least one valid recipient is required."),
                StepRunErrorCategory.Validation);
        }

        // CC and BCC are typed by the editor, so a bad entry fails the step and gets noticed,
        // rather than the email silently going out without that copy.
        var cc = SplitAddresses(settings.Cc);
        var bcc = SplitAddresses(settings.Bcc);
        var invalidCopies = cc.Count(entry => !IsValidAddress(entry)) + bcc.Count(entry => !IsValidAddress(entry));
        if (invalidCopies > 0)
        {
            // A count only: the addresses themselves are personal data.
            return ActionResult.Failed(
                new ArgumentException($"{invalidCopies} CC/BCC {(invalidCopies == 1 ? "address" : "addresses")} could not be parsed."),
                StepRunErrorCategory.Validation);
        }

        var replyTo = ParseReplyTo(settings.ReplyTo, context);

        var emailMessage = new EmailMessage(
            from: null,
            to: recipients,
            cc: cc.Length > 0 ? cc : null,
            bcc: bcc.Length > 0 ? bcc : null,
            replyTo: replyTo,
            subject: settings.Subject,
            body: settings.Body,
            isBodyHtml: settings.IsHtml,
            attachments: null);

        await _emailSender.SendAsync(emailMessage, EmailType, enableNotification: false, expires: null);

        _logger.LogInformation(
            "Automation {AutomationId} / Run {RunId}: Sent email to {RecipientCount} recipient(s)",
            context.AutomationId, context.RunId, recipients.Length);

        // A count only: recipient addresses and the message body are personal data.
        context.LogInfo($"Sent email to {recipients.Length} {(recipients.Length == 1 ? "recipient" : "recipients")}");

        return Success(new SendEmailOutput
        {
            RecipientCount = recipients.Length,
            Subject = settings.Subject,
        });
    }

    /// <summary>
    /// Splits the Reply-To setting and keeps the addresses that parse. Reply-To is usually bound
    /// to visitor input, so a bad entry is skipped with a warning instead of failing the step -
    /// the email still reaches its recipients, just without that reply address.
    /// </summary>
    private string[]? ParseReplyTo(string? value, ActionContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var entries = SplitAddresses(value);
        var valid = entries.Where(IsValidAddress).ToArray();

        var skipped = entries.Length - valid.Length;
        if (skipped > 0)
        {
            _logger.LogWarning(
                "Automation {AutomationId} / Run {RunId}: Skipped {SkippedCount} Reply-To address(es) that could not be parsed",
                context.AutomationId, context.RunId, skipped);

            // A count only, as above: the addresses themselves are personal data.
            context.LogWarning($"Skipped {skipped} Reply-To {(skipped == 1 ? "address" : "addresses")} that could not be parsed");
        }

        return valid.Length > 0 ? valid : null;
    }

    /// <summary>
    /// Splits a comma-separated address list. A comma inside a quoted display name
    /// (<c>"Doe, John" &lt;j@example.com&gt;</c>) does not split it.
    /// </summary>
    private static string[] SplitAddresses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var entries = new List<string>();
        var start = 0;
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            switch (value[i])
            {
                case '\\' when inQuotes:
                    i++; // skip the escaped character
                    break;
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ',' when !inQuotes:
                    entries.Add(value[start..i]);
                    start = i + 1;
                    break;
            }
        }

        entries.Add(value[start..]);
        return entries.Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToArray();
    }

    /// <summary>
    /// An entry is kept only if both .NET's <see cref="MailAddress"/> and MimeKit read it as a
    /// single mailbox. MimeKit is what the CMS's default <see cref="IEmailSender"/> parses with, and
    /// it silently drops what it can't parse; <see cref="MailAddress"/> alone accepts
    /// <c>a@example.com; b@example.com</c> as a display name plus an address, which MimeKit rejects.
    /// Requiring both keeps the check strict whichever sender is registered.
    /// </summary>
    private static bool IsValidAddress(string entry)
        => MailAddress.TryCreate(entry, out _)
           && InternetAddress.TryParse(entry, out var address)
           && address is MailboxAddress;
}
