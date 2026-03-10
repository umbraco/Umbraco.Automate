using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class ActionMiddlewarePipelineTests
{
    private static ActionContext CreateContext() => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "test.action",
    };

    [Fact]
    public async Task ExecuteAsync_NoMiddleware_ExecutesActionDirectly()
    {
        var collection = CreateCollection([]);
        var pipeline = new ActionMiddlewarePipeline(collection);

        var action = new Mock<IAction>();
        action.Setup(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActionResult.Success(new { Value = 42 }));

        var result = await pipeline.ExecuteAsync(action.Object, CreateContext(), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareExecutesInOrder()
    {
        var order = new List<string>();

        var middleware1 = new TestOrderMiddleware("first", order);
        var middleware2 = new TestOrderMiddleware("second", order);

        var collection = CreateCollection([middleware1, middleware2]);
        var pipeline = new ActionMiddlewarePipeline(collection);

        var action = new Mock<IAction>();
        action.Setup(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("action"))
            .ReturnsAsync(ActionResult.Success());

        await pipeline.ExecuteAsync(action.Object, CreateContext(), CancellationToken.None);

        order.ShouldBe(["first-before", "second-before", "action", "second-after", "first-after"]);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareCanShortCircuit()
    {
        var shortCircuit = new ShortCircuitMiddleware();
        var collection = CreateCollection([shortCircuit]);
        var pipeline = new ActionMiddlewarePipeline(collection);

        var action = new Mock<IAction>();

        var result = await pipeline.ExecuteAsync(action.Object, CreateContext(), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Skipped);
        action.Verify(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_MiddlewareCanModifyContext()
    {
        ActionContext? capturedContext = null;
        var middleware = new ContextModifyingMiddleware();
        var collection = CreateCollection([middleware]);
        var pipeline = new ActionMiddlewarePipeline(collection);

        var action = new Mock<IAction>();
        action.Setup(a => a.ExecuteAsync(It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .Callback<ActionContext, CancellationToken>((ctx, _) => capturedContext = ctx)
            .ReturnsAsync(ActionResult.Success());

        var original = CreateContext();
        await pipeline.ExecuteAsync(action.Object, original, CancellationToken.None);

        // The middleware creates a new context with modified alias
        capturedContext.ShouldNotBeNull();
        capturedContext.ActionAlias.ShouldBe("modified.action");
    }

    private static ActionMiddlewareCollection CreateCollection(IActionMiddleware[] items)
        => new(() => items);

    private sealed class TestOrderMiddleware : IActionMiddleware
    {
        private readonly string _name;
        private readonly List<string> _order;

        public TestOrderMiddleware(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }

        public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
        {
            _order.Add($"{_name}-before");
            var result = await next(context, cancellationToken);
            _order.Add($"{_name}-after");
            return result;
        }
    }

    private sealed class ShortCircuitMiddleware : IActionMiddleware
    {
        public Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Skipped("Short-circuited by middleware."));
    }

    private sealed class ContextModifyingMiddleware : IActionMiddleware
    {
        public Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
        {
            var modified = new ActionContext
            {
                AutomationId = context.AutomationId,
                RunId = context.RunId,
                StepId = context.StepId,
                ActionAlias = "modified.action",
                Settings = context.Settings,
                InputData = context.InputData,
            };
            return next(modified, cancellationToken);
        }
    }
}
