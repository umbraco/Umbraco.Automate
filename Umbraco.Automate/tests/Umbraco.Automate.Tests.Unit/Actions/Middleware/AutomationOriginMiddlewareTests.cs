using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class AutomationOriginMiddlewareTests
{
    private readonly AutomationOriginAccessor _accessor = new();
    private readonly AutomationOriginMiddleware _middleware;

    public AutomationOriginMiddlewareTests()
    {
        _middleware = new AutomationOriginMiddleware(_accessor);
    }

    [Fact]
    public async Task ApplyAsync_NoExecutionContext_DelegatesToNextWithoutPushing()
    {
        var context = CreateContext(executionContext: null);
        AutomationOrigin? observed = null;

        await _middleware.ApplyAsync(
            context,
            (_, _) =>
            {
                observed = _accessor.Current;
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        observed.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_WithExecutionContext_PushesOriginWithCurrentAutomationAppended()
    {
        // Top-level run (no upstream chain) — middleware should push a chain containing
        // only the current automation so any side effects can detect a self-loop.
        var executionContext = CreateExecutionContext(originChain: []);
        var context = CreateContext(executionContext);

        AutomationOrigin? observed = null;

        await _middleware.ApplyAsync(
            context,
            (_, _) =>
            {
                observed = _accessor.Current;
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        observed.ShouldNotBeNull();
        observed!.RunId.ShouldBe(executionContext.RunId);
        observed.WorkspaceId.ShouldBe(executionContext.WorkspaceId);
        observed.AutomationChain.ShouldBe([context.AutomationId]);
    }

    [Fact]
    public async Task ApplyAsync_AppendsCurrentAutomationToInheritedChain()
    {
        // Downstream run inheriting an upstream chain — middleware appends the current
        // automation so the resulting chain reflects the full lineage A → B for any
        // side-effect events this run dispatches.
        var upstreamA = Guid.NewGuid();
        var executionContext = CreateExecutionContext(originChain: [upstreamA]);
        var context = CreateContext(executionContext);

        AutomationOrigin? observed = null;

        await _middleware.ApplyAsync(
            context,
            (_, _) =>
            {
                observed = _accessor.Current;
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        observed!.AutomationChain.ShouldBe([upstreamA, context.AutomationId]);
    }

    [Fact]
    public async Task ApplyAsync_ClearsOriginAfterExecution()
    {
        var context = CreateContext(CreateExecutionContext());

        await _middleware.ApplyAsync(
            context,
            (_, _) => Task.FromResult(ActionResult.Success()),
            CancellationToken.None);

        _accessor.Current.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyAsync_ClearsOriginEvenOnException()
    {
        var context = CreateContext(CreateExecutionContext());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _middleware.ApplyAsync(
                context,
                (_, _) => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        _accessor.Current.ShouldBeNull();
    }

    private static AutomationExecutionContext CreateExecutionContext(IReadOnlyList<Guid>? originChain = null) => new()
    {
        ServiceAccountKey = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Test Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Test Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "test",
        AllowedConnections = [],
        OriginChain = originChain ?? [],
    };

    private static ActionContext CreateContext(AutomationExecutionContext? executionContext) => new()
    {
        AutomationId = executionContext?.AutomationId ?? Guid.NewGuid(),
        RunId = executionContext?.RunId ?? Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.test",
        ExecutionContext = executionContext,
    };
}
