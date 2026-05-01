using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
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
            CreateExecutionOptionsMonitor(),
            Mock.Of<ILogger<TriggerEventHandler>>());
    }

    private static IOptionsMonitor<ExecutionOptions> CreateExecutionOptionsMonitor(int maxChainDepth = 5)
    {
        var monitor = new Mock<IOptionsMonitor<ExecutionOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new ExecutionOptions { MaxChainDepth = maxChainDepth });
        return monitor.Object;
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Exactly(2));
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
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<Automation, string, string?, Dictionary<string, object?>?, CancellationToken, IReadOnlyList<Guid>?>(
                (_, _, _, data, _, _) => capturedData = data)
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Once);

        _executor.Verify(e => e.ExecuteAsync(
            nonMatching,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
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
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ChainLengthExceedsLimit_DropsEvent()
    {
        // Belt-and-braces backstop: even if a per-trigger SkipAutomationOriginatedEvents
        // toggle is off, runaway cascades stop here when the chain grows past the limit.
        var handler = new TriggerEventHandler(
            _automationService.Object,
            Mock.Of<IEntityVersionService>(),
            _executor.Object,
            _nodeEligibility.Object,
            _triggers,
            CreateExecutionOptionsMonitor(maxChainDepth: 3),
            Mock.Of<ILogger<TriggerEventHandler>>());

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "system",
            OriginRunId = Guid.NewGuid(),
            OriginAutomationChain = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()],
        });

        await handler.HandleAsync(body, CancellationToken.None);

        _automationService.Verify(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AutomationInChainAndSkipEnabled_SkipsAsCycle()
    {
        // Direct self-loop: A's action saves → event chain contains A → A would re-trigger
        // itself → drop. Models the "save inside an automation re-fires the same automation"
        // scenario the loop-prevention is built for.
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["skipAutomationOriginatedEvents"] = true,
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
            OriginRunId = Guid.NewGuid(),
            OriginAutomationChain = [automationId],
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_PingPongCycleAtoBtoA_SkipsA()
    {
        // Indirect cycle: A's action saved content, B fired, B's action saved again, the
        // resulting chain is [A, B]. A receiving that event finds itself in the chain and
        // skips — proving the chain catches A → B → A patterns where a single-ID compare
        // would have missed it.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var automationA = new AutomationBuilder()
            .WithId(idA)
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["skipAutomationOriginatedEvents"] = true,
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automationA });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
            OriginRunId = Guid.NewGuid(),
            OriginAutomationChain = [idA, idB],
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AutomationNotInChain_RunsWithChainPropagated()
    {
        // Pure observer: B is listening to content saves but isn't part of any cycle
        // upstream. Chain is [A], B is not in it, so B runs — and inherits the chain so
        // its own side effects extend it correctly.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var automationB = new AutomationBuilder()
            .WithId(idB)
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["skipAutomationOriginatedEvents"] = true,
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automationB });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
            OriginRunId = Guid.NewGuid(),
            OriginAutomationChain = [idA],
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automationB,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.Is<IReadOnlyList<Guid>?>(c => c != null && c.SequenceEqual(new[] { idA }))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AutomationInChainButSkipDisabled_RunsAnyway()
    {
        // Operator deliberately turned the toggle off — chain membership doesn't matter,
        // run it. Depth backstop is the only remaining safety net here.
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["skipAutomationOriginatedEvents"] = false,
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
            OriginRunId = Guid.NewGuid(),
            OriginAutomationChain = [automationId],
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NoOriginChain_RunsAutomationWithEmptyChain()
    {
        // User-initiated save: chain is empty, automation runs and inherits empty chain
        // (its own actions will be the start of any new cascade).
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["skipAutomationOriginatedEvents"] = true,
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "user",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.Is<IReadOnlyList<Guid>?>(c => c != null && c.Count == 0)), Times.Once);
    }

    private static ContentSavedTriggerOutput BuildContentSavedOutput() => new()
    {
        ContentKey = Guid.NewGuid(),
        ContentName = "Page",
        ContentTypeKey = Guid.NewGuid(),
        ContentTypeAlias = "blogPost",
    };

    private static string SerializeMessage(TriggerEventMessage message)
        => JsonSerializer.Serialize(message, JsonOptions.Default);

    private static Automation CreatePublishedAutomation(string triggerAlias) =>
        new AutomationBuilder()
            .WithTrigger(triggerAlias)
            .Build();
}
