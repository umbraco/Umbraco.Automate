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

    private static SendEmailSettings Settings(string? replyTo = null) => new()
    {
        To = "owner@example.com",
        Subject = "New booking",
        Body = "<p>Hello</p>",
        ReplyTo = replyTo,
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
        context.LogEntries.ShouldContain(e => e.Level == ActionLogLevel.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyUnparseableReplyTo_SendsWithoutReplyTo()
    {
        var result = await _action.ExecuteAsync(CreateContext(Settings("not an address")), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _sent.ShouldNotBeNull();
        _sent.ReplyTo.ShouldBeNull();
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

    [Fact]
    public void ReplyTo_SupportsBindings()
    {
        var field = typeof(SendEmailSettings).GetProperty(nameof(SendEmailSettings.ReplyTo))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

        field.SupportsBindings.ShouldBeTrue();
    }
}
