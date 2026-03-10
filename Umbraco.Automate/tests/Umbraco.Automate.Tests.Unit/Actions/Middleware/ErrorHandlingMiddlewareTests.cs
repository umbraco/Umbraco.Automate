using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class ErrorHandlingMiddlewareTests
{
    private readonly ErrorHandlingMiddleware _middleware;

    public ErrorHandlingMiddlewareTests()
    {
        _middleware = new ErrorHandlingMiddleware(Mock.Of<ILogger<ErrorHandlingMiddleware>>());
    }

    private static ActionContext CreateContext() => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "test.action",
    };

    [Fact]
    public async Task ApplyAsync_Success_PassesThrough()
    {
        var expected = ActionResult.Success(new { Value = 1 });

        var result = await _middleware.ApplyAsync(
            CreateContext(),
            (_, _) => Task.FromResult(expected),
            CancellationToken.None);

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task ApplyAsync_UnhandledException_ReturnsFailed()
    {
        var result = await _middleware.ApplyAsync(
            CreateContext(),
            (_, _) => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.Exception.ShouldBeOfType<InvalidOperationException>();
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
    }

    [Fact]
    public async Task ApplyAsync_TimeoutException_ReturnsCategorisedFailure()
    {
        var result = await _middleware.ApplyAsync(
            CreateContext(),
            (_, _) => throw new TimeoutException("timed out"),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Timeout);
    }

    [Fact]
    public async Task ApplyAsync_CancellationRequested_ReturnsCancelledFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _middleware.ApplyAsync(
            CreateContext(),
            (_, ct) => throw new OperationCanceledException(ct),
            cts.Token);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }
}
