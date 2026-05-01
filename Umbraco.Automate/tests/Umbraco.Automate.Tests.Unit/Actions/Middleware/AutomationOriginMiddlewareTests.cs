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
    public async Task ApplyAsync_WithExecutionContext_PushesOrigin()
    {
        var executionContext = CreateExecutionContext(chainDepth: 0);
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
        observed.AutomationId.ShouldBe(executionContext.AutomationId);
        observed.WorkspaceId.ShouldBe(executionContext.WorkspaceId);
        observed.ChainDepth.ShouldBe(0);
    }

    [Fact]
    public async Task ApplyAsync_PropagatesChainDepth()
    {
        var executionContext = CreateExecutionContext(chainDepth: 3);
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

        observed!.ChainDepth.ShouldBe(3);
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

    private static AutomationExecutionContext CreateExecutionContext(int chainDepth = 0) => new()
    {
        ServiceAccountKey = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Test Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Test Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "test",
        AllowedConnections = [],
        ChainDepth = chainDepth,
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
