using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing.Builders;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class TriggerEventHandlerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<IExecutionNodeEligibility> _nodeEligibility = new();
    private readonly TriggerCollection _triggers;
    private readonly TriggerEventHandler _handler;

    public TriggerEventHandlerTests()
    {
        _nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        var modelResolver = new EditableModelResolver(new ConfigurationBuilder().Build());
        _triggers = new TriggerCollection(() =>
        {
            var infra = new TriggerInfrastructure(modelResolver);
            return new ITrigger[]
            {
                new ContentSavedTrigger(infra),
                new ContentPublishedTrigger(infra),
            };
        });

        _handler = new TriggerEventHandler(
            _automationService.Object,
            Mock.Of<IEntityVersionService>(),
            _executor.Object,
            _nodeEligibility.Object,
            _triggers,
            Mock.Of<ILogger<TriggerEventHandler>>());
    }

    [Fact]
    public async Task HandleAsync_MatchingAutomation_ExecutesRun()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            "system",
            null,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoMatchingAutomations_DoesNotExecute()
    {
        var automation = CreatePublishedAutomation("otherTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_DraftAutomation_DoesNotExecute()
    {
        Automation automation = new AutomationBuilder()
            .WithTrigger("myTrigger")
            .AsDraft();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MultipleMatchingAutomations_ExecutesAll()
    {
        var a1 = CreatePublishedAutomation("myTrigger");
        var a2 = CreatePublishedAutomation("myTrigger");

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { a1, a2 });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithOutputData_DeserializesAndPassesToExecutor()
    {
        var automation = CreatePublishedAutomation("myTrigger");
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        Dictionary<string, object?>? capturedData = null;
        _executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Automation, string, string?, Dictionary<string, object?>?, CancellationToken>(
                (_, _, _, data, _) => capturedData = data)
            .ReturnsAsync(Guid.NewGuid());

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
            OutputData = "{\"contentName\":\"Hello\"}",
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        capturedData.ShouldNotBeNull();
        capturedData.ShouldContainKey("contentName");
    }

    [Fact]
    public async Task HandleAsync_NodeIneligible_ThrowsNodeNotEligibleException()
    {
        // Defensive race-guard: eligibility flipped between the dispatcher's pre-claim
        // filter and HandleAsync. Throw so the dispatcher releases the claim back to
        // Pending (rather than silently completing and losing the trigger event).
        _nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(false);

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
        });

        await Should.ThrowAsync<NodeNotEligibleException>(
            () => _handler.HandleAsync(body, CancellationToken.None));

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CanProcessNow_DelegatesToEligibilityService()
    {
        _nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(false);
        _handler.CanProcessNow().ShouldBeFalse();

        _nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);
        _handler.CanProcessNow().ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_TriggerSettingsFilter_SkipsNonMatchingAutomation()
    {
        var allowedTypeKey = Guid.NewGuid();
        var blockedTypeKey = Guid.NewGuid();

        var matching = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["contentTypes"] = allowedTypeKey.ToString(),
            })
            .Build();

        var nonMatching = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["contentTypes"] = blockedTypeKey.ToString(),
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matching, nonMatching });

        var output = new ContentSavedTriggerOutput
        {
            ContentKey = Guid.NewGuid(),
            ContentName = "Page",
            ContentTypeKey = allowedTypeKey,
            ContentTypeAlias = "blogPost",
        };

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(output, JsonOptions.Default),
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            matching,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _executor.Verify(e => e.ExecuteAsync(
            nonMatching,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TriggerSettingsFilter_EmptyFilterMatchesAll()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved")
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var output = new ContentSavedTriggerOutput
        {
            ContentKey = Guid.NewGuid(),
            ContentName = "Page",
            ContentTypeKey = Guid.NewGuid(),
            ContentTypeAlias = "blogPost",
        };

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(output, JsonOptions.Default),
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string SerializeMessage(TriggerEventMessage message)
        => JsonSerializer.Serialize(message, JsonOptions.Default);

    private static Automation CreatePublishedAutomation(string triggerAlias) =>
        new AutomationBuilder()
            .WithTrigger(triggerAlias)
            .Build();
}
