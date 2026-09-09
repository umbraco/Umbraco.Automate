using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class TriggerEventHandlerTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<IExecutionNodeEligibility> _nodeEligibility = new();
    private readonly Mock<IWorkspaceServiceAccountResolver> _serviceAccountResolver = new();
    private readonly Mock<IAutomationActionAuthorizer> _nodeAuthorizer = new();
    private readonly TriggerCollection _triggers;
    private readonly TriggerDispatchAuthorizerCollection _dispatchAuthorizers;
    private readonly TriggerEventHandler _handler;

    public TriggerEventHandlerTests()
    {
        _nodeEligibility.Setup(e => e.CanExecuteWorkflows()).Returns(true);

        // Default service-account resolver so the runtime section guard passes.
        // Individual tests override to exercise the deny path.
        _serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "content", "media", "members", "users" }));

        // Default node authoriser passes for all keys. Tests that exercise the node-level
        // deny path override per-key.
        _nodeAuthorizer.Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);
        _nodeAuthorizer.Setup(a => a.AuthorizeMediaAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        _triggers = new TriggerCollection(() =>
        {
            var infra = new TriggerInfrastructure(modelResolver);
            return new ITrigger[]
            {
                new ContentSavedTrigger(infra),
                new ContentPublishedTrigger(infra, Mock.Of<IUserService>(), Mock.Of<ILogger<ContentPublishedTrigger>>()),
            };
        });

        // Production DI registers NodeScopedTriggerDispatchAuthorizer plus any provider
        // package additions. Tests use only the built-in — it's what the unit suite was
        // already exercising via the (now-internal) inline node guard.
        _dispatchAuthorizers = new TriggerDispatchAuthorizerCollection(() => new ITriggerDispatchAuthorizer[]
        {
            new NodeScopedTriggerDispatchAuthorizer(_nodeAuthorizer.Object),
        });

        _handler = new TriggerEventHandler(
            _automationService.Object,
            Mock.Of<IEntityVersionService>(),
            _executor.Object,
            _nodeEligibility.Object,
            _triggers,
            _serviceAccountResolver.Object,
            new SectionAccessChecker(),
            _dispatchAuthorizers,
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
    public async Task HandleAsync_TargetAutomationId_RunsOnlyTargetedAutomation()
    {
        // Two published automations share the alias (the webhook fan-out scenario). The event
        // targets exactly one — only that automation should run, never the sibling.
        var target = new AutomationBuilder().WithId(Guid.NewGuid()).WithTrigger("myTrigger").Build();
        var sibling = new AutomationBuilder().WithId(Guid.NewGuid()).WithTrigger("myTrigger").Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { target, sibling });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "webhook",
            TargetAutomationId = target.Id,
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            target,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Once);

        _executor.Verify(e => e.ExecuteAsync(
            sibling,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TargetAutomationId_NoMatch_DoesNotExecute()
    {
        // The targeted automation isn't among the published set (deleted/unpublished since the
        // event was queued) — drop rather than fall back to firing the alias siblings.
        var sibling = CreatePublishedAutomation("myTrigger");

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sibling });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "myTrigger",
            InitiatorType = "webhook",
            TargetAutomationId = Guid.NewGuid(),
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
    public async Task HandleAsync_PublishedByFilter_SkipsNonMatchingAutomation()
    {
        var humanOnly = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished", new Dictionary<string, object?>
            {
                ["publishedBy"] = nameof(PublishedByFilter.User),
            })
            .Build();

        var systemOnly = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentPublished", new Dictionary<string, object?>
            {
                ["publishedBy"] = nameof(PublishedByFilter.System),
            })
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { humanOnly, systemOnly });

        // Publish performed by an automation service account (API user).
        var output = new ContentPublishedTriggerOutput
        {
            ContentKey = Guid.NewGuid(),
            ContentName = "Page",
            ContentTypeKey = Guid.NewGuid(),
            ContentTypeAlias = "blogPost",
            PublisherId = 42,
            PublisherKind = ContentPublisherKind.Api,
        };

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentPublished",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(output, JsonOptions.Default),
        });

        await _handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            systemOnly,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Once);

        _executor.Verify(e => e.ExecuteAsync(
            humanOnly,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
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
            _serviceAccountResolver.Object,
            new SectionAccessChecker(),
            _dispatchAuthorizers,
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
    public async Task HandleAsync_SkipOnCycle_AutomationInChain_SkipsAsCycle()
    {
        // Direct self-loop: A's action saves → event chain contains A → A would re-trigger
        // itself → drop. Models the "save inside an automation re-fires the same automation"
        // scenario the loop-prevention is built for.
        var automationId = Guid.NewGuid();
        var automation = BuildContentSavedAutomation(automationId, AutomationOriginatedEventBehavior.SkipOnCycle);

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
    public async Task HandleAsync_SkipOnCycle_PingPongAtoBtoA_SkipsA()
    {
        // Indirect cycle: A's action saved content, B fired, B's action saved again, the
        // resulting chain is [A, B]. A receiving that event finds itself in the chain and
        // skips — proving the chain catches A → B → A patterns where a single-ID compare
        // would have missed it.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var automationA = BuildContentSavedAutomation(idA, AutomationOriginatedEventBehavior.SkipOnCycle);

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
    public async Task HandleAsync_SkipOnCycle_AutomationNotInChain_RunsWithChainPropagated()
    {
        // Pure observer: B is listening to content saves but isn't part of any cycle
        // upstream. Chain is [A], B is not in it, so B runs — and inherits the chain so
        // its own side effects extend it correctly. SkipAlways behavior would block this;
        // SkipOnCycle is the more precise default.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var automationB = BuildContentSavedAutomation(idB, AutomationOriginatedEventBehavior.SkipOnCycle);

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
            It.Is<IReadOnlyList<Guid>>(c => c != null && c.SequenceEqual(new[] { idA }))), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SkipAlways_BlocksAnyAutomationOriginatedEvent()
    {
        // Strict mode: this trigger should only fire on external input. Any chain — even
        // one that doesn't contain this automation — drops the event.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var automationB = BuildContentSavedAutomation(idB, AutomationOriginatedEventBehavior.SkipAlways);

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
            It.IsAny<Automation>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Run_IgnoresChainAndAlwaysFires()
    {
        // Operator opted into "Run" — chains are intentional, fire even when this automation
        // is in the chain. Chain-length backstop is the only remaining safety net.
        var automationId = Guid.NewGuid();
        var automation = BuildContentSavedAutomation(automationId, AutomationOriginatedEventBehavior.Run);

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
    public async Task HandleAsync_NoOriginChain_RunsRegardlessOfBehavior()
    {
        // User-initiated save: no chain → no origin-aware filtering applies. SkipAlways
        // explicitly should still permit this case because no automation caused the event.
        var automation = BuildContentSavedAutomation(Guid.NewGuid(), AutomationOriginatedEventBehavior.SkipAlways);

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
            It.Is<IReadOnlyList<Guid>>(c => c != null && c.Count == 0)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ServiceAccountLacksNodeAccess_SkipsAutomation()
    {
        // Trigger emits a Content node the service account is forbidden to access (outside
        // its start-node path). Section access alone passes; node-level guard must reject.
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved")
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var triggerOutput = BuildContentSavedOutput();
        _nodeAuthorizer.Setup(a => a.AuthorizeContentAsync(
                It.IsAny<IUser>(),
                triggerOutput.ContentKey,
                It.IsAny<IReadOnlySet<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(triggerOutput, JsonOptions.Default),
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
    public async Task HandleAsync_ServiceAccountLacksTriggerSection_SkipsAutomation()
    {
        // Trigger requires `content`; service account only has `media` → runtime guard skips
        // the automation without starting a run. Models post-publish service-account downgrade.
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved")
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        _serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IUser>(u => u.AllowedSections == new[] { "media" }));

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
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
    public async Task HandleAsync_ServiceAccountMissing_SkipsAutomationWithSectionedTrigger()
    {
        var automation = new AutomationBuilder()
            .WithTrigger("umbracoAutomate.contentSaved")
            .Build();

        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        _serviceAccountResolver.Setup(r => r.GetServiceAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser?)null);

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
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
    public async Task HandleAsync_FirstAuthorizerDenial_ShortCircuitsRemainingAuthorizers()
    {
        // Two authorisers, both would deny — but only the first should be consulted. Proves
        // the collection iteration short-circuits on first failure rather than running the
        // whole chain.
        var first = new RecordingAuthorizer(deny: true, reason: "first");
        var second = new RecordingAuthorizer(deny: true, reason: "second");

        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        var triggers = new TriggerCollection(() => new ITrigger[]
        {
            new ContentSavedTrigger(new TriggerInfrastructure(modelResolver)),
        });

        var authorisers = new TriggerDispatchAuthorizerCollection(
            () => new ITriggerDispatchAuthorizer[] { first, second });

        var handler = new TriggerEventHandler(
            _automationService.Object,
            Mock.Of<IEntityVersionService>(),
            _executor.Object,
            _nodeEligibility.Object,
            triggers,
            _serviceAccountResolver.Object,
            new SectionAccessChecker(),
            authorisers,
            CreateExecutionOptionsMonitor(),
            Mock.Of<ILogger<TriggerEventHandler>>());

        var automation = new AutomationBuilder().WithTrigger("umbracoAutomate.contentSaved").Build();
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
        });

        await handler.HandleAsync(body, CancellationToken.None);

        first.Calls.ShouldBe(1);
        second.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task HandleAsync_EmptyAuthorizerCollection_AllowsDispatch()
    {
        // Section gate is enough on its own: with no authorisers registered, a trigger that
        // passes section access (or has no section requirements) should dispatch normally.
        // Matches the manual / scheduled trigger code path where no node-scoping applies.
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        var triggers = new TriggerCollection(() => new ITrigger[]
        {
            new ContentSavedTrigger(new TriggerInfrastructure(modelResolver)),
        });

        var authorisers = new TriggerDispatchAuthorizerCollection(
            () => Array.Empty<ITriggerDispatchAuthorizer>());

        var handler = new TriggerEventHandler(
            _automationService.Object,
            Mock.Of<IEntityVersionService>(),
            _executor.Object,
            _nodeEligibility.Object,
            triggers,
            _serviceAccountResolver.Object,
            new SectionAccessChecker(),
            authorisers,
            CreateExecutionOptionsMonitor(),
            Mock.Of<ILogger<TriggerEventHandler>>());

        var automation = new AutomationBuilder().WithTrigger("umbracoAutomate.contentSaved").Build();
        _automationService.Setup(s => s.GetAllAutomationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { automation });

        var body = SerializeMessage(new TriggerEventMessage
        {
            TriggerAlias = "umbracoAutomate.contentSaved",
            InitiatorType = "system",
            OutputData = JsonSerializer.Serialize(BuildContentSavedOutput(), JsonOptions.Default),
        });

        await handler.HandleAsync(body, CancellationToken.None);

        _executor.Verify(e => e.ExecuteAsync(
            automation,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
    }

    private sealed class RecordingAuthorizer : ITriggerDispatchAuthorizer
    {
        private readonly bool _deny;
        private readonly string _reason;

        public RecordingAuthorizer(bool deny, string reason)
        {
            _deny = deny;
            _reason = reason;
        }

        public int Calls { get; private set; }

        public Task<AutomationAuthorizationResult> AuthorizeAsync(
            TriggerDispatchAuthorizationContext context,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_deny
                ? AutomationAuthorizationResult.Fail(_reason)
                : AutomationAuthorizationResult.Success);
        }
    }

    private static Automation BuildContentSavedAutomation(Guid id, AutomationOriginatedEventBehavior behavior)
        => new AutomationBuilder()
            .WithId(id)
            .WithTrigger("umbracoAutomate.contentSaved", new Dictionary<string, object?>
            {
                ["onAutomationOriginatedEvent"] = behavior.ToString(),
            })
            .Build();

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
