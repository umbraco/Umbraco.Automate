using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// The single MCP tool an automation's <see cref="McpTrigger"/> exposes at its own URL.
/// Executes the automation and polls <see cref="IAutomationRunService"/> for the result rather
/// than the in-process completion notification, so this stays correct when Automate runs on
/// more than one instance — the run may finalize on an instance other than the one holding this
/// request open.
/// </summary>
internal sealed class AutomationMcpTool : McpServerTool
{
    private readonly Automation _automation;
    private readonly McpTriggerSettings _settings;
    private readonly IAutomationExecutor _executor;
    private readonly IAutomationRunService _runService;
    private readonly TimeSpan _pollInterval;

    public AutomationMcpTool(
        Automation automation,
        McpTriggerSettings settings,
        IAutomationExecutor executor,
        IAutomationRunService runService,
        TimeSpan? pollInterval = null)
    {
        _automation = automation;
        _settings = settings;
        _executor = executor;
        _runService = runService;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);

        ProtocolTool = new Tool
        {
            Name = settings.ToolName,
            Description = settings.ToolDescription,
            InputSchema = McpToolSchemaBuilder.BuildInputSchema(settings.InputFields),
        };
    }

    public override Tool ProtocolTool { get; }

    /// <summary>
    /// No extra metadata beyond the protocol <see cref="Tool"/> itself — this tool isn't built
    /// from an <c>AIFunction</c>, so there's nothing to surface here.
    /// </summary>
    public override IReadOnlyList<object> Metadata { get; } = [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        if (!McpToolArgumentBinder.TryBind(_settings.InputFields, request.Params?.Arguments, out var arguments, out var bindError))
        {
            return ErrorResult(bindError!);
        }

        var output = new McpTriggerOutput { Arguments = arguments };
        var triggerOutputData = JsonOptions.DeserializeToUnwrappedDictionary(
            JsonSerializer.Serialize(output, JsonOptions.Default));

        var runId = await _executor.ExecuteAsync(
            _automation,
            TriggerInitiatorType.AiAgent,
            initiatorId: null,
            triggerOutputData,
            cancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(_settings.TimeoutSeconds);
        AutomationRun? run;
        while (true)
        {
            run = await _runService.GetRunAsync(runId, cancellationToken);
            if (run is not null && IsTerminal(run.Status))
            {
                break;
            }

            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        if (run is null || !IsTerminal(run.Status))
        {
            return new CallToolResult
            {
                IsError = false,
                Content = [new TextContentBlock
                {
                    Text = $"Still running after {_settings.TimeoutSeconds}s. Run ID: {runId}.",
                }],
            };
        }

        if (run.Status is AutomationRunStatus.Failed or AutomationRunStatus.Rejected)
        {
            return ErrorResult(run.Error ?? $"Run ended as {run.Status}.");
        }

        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = BuildResultText(run) }],
        };
    }

    private static bool IsTerminal(AutomationRunStatus status)
        => status is AutomationRunStatus.Completed or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled or AutomationRunStatus.Rejected;

    private static string BuildResultText(AutomationRun run)
    {
        var lastOutput = run.StepRuns
            .LastOrDefault(sr => sr.Status == StepRunStatus.Completed && sr.OutputData is not null);

        return lastOutput?.OutputData ?? "Automation completed.";
    }

    private static CallToolResult ErrorResult(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };
}
