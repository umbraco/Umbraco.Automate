using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class SendEmailActionTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly SendEmailAction _action;
    private EmailMessage? _sent;

    public SendEmailActionTests()
    {
        _emailSender.Setup(x => x.CanSendRequiredEmail()).Returns(true);
        _emailSender
            .Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
            .Callback<EmailMessage, string, bool, TimeSpan?>((message, _, _, _) => _sent = message)
            .Returns(Task.CompletedTask);

        _action = new SendEmailAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _emailSender.Object,
            Mock.Of<ILogger<SendEmailAction>>());
    }

    private static ActionContext CreateContext(SendEmailSettings settings) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.sendEmail",
        Settings = settings,
    };

    private static SendEmailSettings Settings(string? replyTo = null, string? cc = null, string? bcc = null) => new()
    {
        To = "owner@example.com",
        Subject = "New booking",
        Body = "<p>Hello</p>",
        ReplyTo = replyTo,
        Cc = cc,
        Bcc = bcc,
    };

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.sendEmail");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(SendEmailSettings));

    [Fact]
    public async Task ExecuteAsync_SendsToRecipients()
    {
        var result = await _action.ExecuteAsync(CreateContext(Settings()), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.To.ShouldBe(["owner@example.com"]);
        _sent.Subject.ShouldBe("New booking");
        _sent.IsBodyHtml.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithoutReplyTo_SetsNoReplyTo(string? replyTo)
    {
        await _action.ExecuteAsync(CreateContext(Settings(replyTo)), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithReplyTo_SetsReplyTo()
    {
        await _action.ExecuteAsync(CreateContext(Settings("visitor@example.com")), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBe(["visitor@example.com"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSeveralReplyToAddresses_SplitsAndTrims()
    {
        await _action.ExecuteAsync(CreateContext(Settings(" a@example.com , b@example.com,")), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBe(["a@example.com", "b@example.com"]);
    }

    // Reply-To is usually bound to visitor input. A value that does not parse must not stop the
    // email reaching its recipients - the bad entry is dropped and a warning is logged on the run.
    [Fact]
    public async Task ExecuteAsync_WithUnparseableReplyTo_SkipsItAndStillSends()
    {
        var context = CreateContext(Settings("not an address, visitor@example.com"));

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBe(["visitor@example.com"]);

        // A count only: the skipped value is visitor input, so it must not reach the run log.
        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning && e.Message.Contains("Skipped 1 Reply-To address"));
        context.LogEntries.ShouldAllBe(e => !e.Message.Contains("not an address"));
    }

    // Each of these reads as a display name plus an address to MailAddress, but MimeKit - which the
    // CMS sends with - rejects it and would silently drop the Reply-To. A group address and an
    // address without a domain are accepted by MimeKit only. All must be skipped with a warning.
    [Theory]
    [InlineData("visitor@example.com; other@example.com")]
    [InlineData("visitor@example.com other@example.com")]
    [InlineData("Team: visitor@example.com;")]
    [InlineData("visitor")]
    public async Task ExecuteAsync_WithReplyToTheSenderWouldDrop_SkipsItWithAWarning(string replyTo)
    {
        var context = CreateContext(Settings(replyTo));

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBeNull();
        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning);
        context.LogEntries.ShouldAllBe(e => !e.Message.Contains("visitor"));
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyUnparseableReplyTo_SendsWithoutReplyTo()
    {
        var context = CreateContext(Settings("not an address"));

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBeNull();
        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning);
        context.LogEntries.ShouldAllBe(e => !e.Message.Contains("not an address"));
    }

    [Fact]
    public async Task ExecuteAsync_WithACommaInAQuotedDisplayName_KeepsTheAddressWhole()
    {
        var context = CreateContext(Settings("\"Doe, John\" <j@example.com>, visitor@example.com"));

        await _action.ExecuteAsync(context, CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBe(["\"Doe, John\" <j@example.com>", "visitor@example.com"]);
        context.LogEntries.ShouldNotContain(e => e.Level == ActionLogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WithACommaInAQuotedDisplayNameInTo_KeepsTheRecipientWhole()
    {
        var settings = Settings();
        settings.To = "\"Doe, John\" <j@example.com>, owner@example.com";

        await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.To.ShouldBe(["\"Doe, John\" <j@example.com>", "owner@example.com"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WithoutCcOrBcc_SetsNeither(string? value)
    {
        await _action.ExecuteAsync(CreateContext(Settings(cc: value, bcc: value)), CancellationToken.None);

        _sent.ShouldNotBeNull();
        _sent.Cc.ShouldBeNull();
        _sent.Bcc.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithCcAndBcc_SplitsAndSetsBoth()
    {
        var settings = Settings(cc: " team@example.com , \"Doe, John\" <j@example.com>", bcc: "archive@example.com,");

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.Cc.ShouldBe(["team@example.com", "\"Doe, John\" <j@example.com>"]);
        _sent.Bcc.ShouldBe(["archive@example.com"]);
    }

    // CC and BCC are usually typed by the editor, so a typo fails the step instead of the email
    // silently going out without that copy. Nothing is sent, and the error gives a count only.
    [Theory]
    [InlineData("team@example.com; archive@example.com", null)]
    [InlineData(null, "archive@")]
    [InlineData(null, "archive")]
    public async Task ExecuteAsync_WithUnparseableCcOrBcc_FailsValidationWithoutSending(string? cc, string? bcc)
    {
        var context = CreateContext(Settings(cc: cc, bcc: bcc));

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        result.Exception.ShouldNotBeNull();
        result.Exception.Message.ShouldBe("1 CC/BCC address could not be parsed.");
        _sent.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutRecipients_FailsValidation()
    {
        var settings = Settings();
        settings.To = " ";

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        _sent.ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(SendEmailSettings.ReplyTo))]
    [InlineData(nameof(SendEmailSettings.Cc))]
    [InlineData(nameof(SendEmailSettings.Bcc))]
    public void AddressFields_SupportBindings(string property)
    {
        var field = typeof(SendEmailSettings).GetProperty(property)!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.SupportsBindings.ShouldBeTrue();
    }
}
