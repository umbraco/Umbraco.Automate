using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
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

    private AutomationMcpTool BuildTool(McpTriggerSettings settings)
    {
        var automation = new AutomationBuilder().WithTrigger(McpTrigger.WellKnownAlias);
        return new AutomationMcpTool(
            automation, settings, _executor.Object, _runService.Object, pollInterval: TimeSpan.FromMilliseconds(1));
    }

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
            e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RunCompletesBeforeTimeout_ReturnsSuccessResult()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Completed,
                StepRuns = [new StepRun
                {
                    RunId = runId,
                    StepId = Guid.NewGuid(),
                    ActionAlias = "umbracoAutomate.logMessage",
                    Status = StepRunStatus.Completed,
                    OutputData = """{"message":"done"}""",
                }],
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldContain("done");
    }

    [Fact]
    public async Task InvokeAsync_RunFails_ReturnsErrorWithRunMessage()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Failed,
                Error = "Step 2 blew up",
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("Step 2 blew up");
    }

    [Fact]
    public async Task InvokeAsync_CircuitBreakerTripped_ReturnsErrorImmediately()
    {
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(Guid.Empty);
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain("circuit breaker");
        _runService.Verify(s => s.GetRunAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RunCancelled_ReturnsErrorWithCancelledMessage()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Cancelled,
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain("Cancelled");
    }

    [Fact]
    public async Task InvokeAsync_RunStillRunningAtTimeout_ReturnsNonErrorStillRunningResult()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Running,
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 0 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain(runId.ToString());
    }
}
