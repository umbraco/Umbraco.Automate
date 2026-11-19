using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class AutomationMcpToolTests
{
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<IAutomationRunService> _runService = new();
    private readonly Guid _runId = Guid.NewGuid();

    // The real sanitizer, so the tests prove what reaches the caller is masked, not just that a
    // sanitizer was called.
    private readonly RunDataSanitizer _sanitizer = new(
        new ActionCollection(() => []),
        new ControlFlowCollection(() => []),
        NullLogger<RunDataSanitizer>.Instance);

    private AutomationMcpTool BuildTool(McpTriggerSettings settings)
    {
        var automation = new AutomationBuilder().WithTrigger(McpTrigger.WellKnownAlias);
        return new AutomationMcpTool(
            automation, settings, _executor.Object, _runService.Object, _sanitizer, initialPollInterval: TimeSpan.FromMilliseconds(1));
    }

    private void SetupExecuteReturns(Guid runId)
        => _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);

    private void SetupExecuteThrows(Exception exception)
        => _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ThrowsAsync(exception);

    private void SetupRun(AutomationRunStatus status, string? error = null, params StepRun[] stepRuns)
    {
        _runService
            .Setup(s => s.GetRunStatusAsync(_runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        _runService
            .Setup(s => s.GetRunAsync(_runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = status,
                Error = error,
                StepRuns = [.. stepRuns],
            });
    }

    private StepRun CompletedStep(string outputData, DateTime? completedUtc = null) => new()
    {
        RunId = _runId,
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Status = StepRunStatus.Completed,
        StartedUtc = completedUtc?.AddSeconds(-1),
        CompletedUtc = completedUtc,
        OutputData = outputData,
    };

    // ModelContextProtocol.Server.RequestContext<T> has no single-argument constructor — it
    // requires a JsonRpcRequest (whose `Method` is a required member) alongside the McpServer
    // and the typed parameters, and its base constructor null-checks the server, so a mocked
    // instance is needed rather than a literal null. AutomationMcpTool never touches the server.
    private static RequestContext<CallToolRequestParams> Request(string json)
        => new(
            new Mock<McpServer>().Object,
            new JsonRpcRequest { Method = "tools/call" },
            new CallToolRequestParams
            {
                Name = "tool",
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json),
            });

    private static string Text(CallToolResult result) => result.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    public async Task InvokeAsync_MissingRequiredArgument_ReturnsErrorWithoutExecuting()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "email", Type = McpToolInputFieldType.Text, Required = true }],
        };
        var tool = BuildTool(settings);

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        _executor.Verify(
            e => e.ExecuteAsync(It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RunCompletesBeforeTimeout_ReturnsSuccessResult()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Completed, stepRuns: CompletedStep("""{"message":"done"}"""));
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        Text(result).ShouldContain("done");
    }

    [Fact]
    public async Task InvokeAsync_PollsTheStatusAndLoadsTheFullRunOnce()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Completed, stepRuns: CompletedStep("""{"message":"done"}"""));
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        await tool.InvokeAsync(Request("{}"));

        _runService.Verify(s => s.GetRunStatusAsync(_runId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _runService.Verify(s => s.GetRunAsync(_runId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotPassTheRequestTokenToTheExecutor()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Completed);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });
        using var requestCancellation = new CancellationTokenSource();

        await tool.InvokeAsync(Request("{}"), requestCancellation.Token);

        _executor.Verify(
            e => e.ExecuteAsync(It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object?>?>(), CancellationToken.None, It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_OutputHoldsASecret_MasksItInTheResult()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Completed, stepRuns: CompletedStep("""{"status":200,"access_token":"super-secret-value"}"""));
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        Text(result).ShouldNotContain("super-secret-value");
        Text(result).ShouldContain("200");
    }

    [Fact]
    public async Task InvokeAsync_OutputIsVeryLarge_TruncatesItAndSaysSo()
    {
        SetupExecuteReturns(_runId);
        var large = JsonSerializer.Serialize(new { body = new string('x', IRunDataSanitizer.MaxValueLength * 2) });
        SetupRun(AutomationRunStatus.Completed, stepRuns: CompletedStep(large));
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        Text(result).Length.ShouldBeLessThan(IRunDataSanitizer.MaxValueLength + 200);
        Text(result).ShouldContain("truncated");
    }

    [Fact]
    public async Task InvokeAsync_SeveralStepsFinished_ReturnsTheOutputOfTheOneThatFinishedLast()
    {
        SetupExecuteReturns(_runId);
        var now = DateTime.UtcNow;
        SetupRun(
            AutomationRunStatus.Completed,
            stepRuns:
            [
                CompletedStep("""{"branch":"finished-last"}""", now),
                CompletedStep("""{"branch":"finished-first"}""", now.AddSeconds(-5)),
            ]);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        Text(result).ShouldContain("finished-last");
    }

    [Fact]
    public async Task InvokeAsync_RunFails_ReturnsErrorWithRunMessage()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Failed, error: "Step 2 blew up");
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        Text(result).ShouldBe("Step 2 blew up");
    }

    [Fact]
    public async Task InvokeAsync_CircuitBreakerTripped_ReturnsErrorImmediately()
    {
        SetupExecuteReturns(Guid.Empty);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("circuit breaker");
        _runService.Verify(s => s.GetRunStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RunRateLimited_ReturnsBackOffError()
    {
        SetupExecuteThrows(new RateLimitExceededException(Guid.NewGuid(), "Too many concurrent runs."));
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("try again");
    }

    [Fact]
    public async Task InvokeAsync_RunCancelled_ReturnsErrorWithCancelledMessage()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Cancelled);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        Text(result).ShouldContain("Cancelled");
    }

    [Fact]
    public async Task InvokeAsync_RunStillRunningAtTimeout_ReturnsNonErrorStillRunningResult()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Running);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 1 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        Text(result).ShouldContain(_runId.ToString());
    }

    [Fact]
    public async Task InvokeAsync_TimeoutBelowOneSecond_WaitsOneSecond()
    {
        SetupExecuteReturns(_runId);
        SetupRun(AutomationRunStatus.Running);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = -5 });

        var result = await tool.InvokeAsync(Request("{}"));

        Text(result).ShouldStartWith("Still running after 1s.");
    }
}
