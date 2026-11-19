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
/// <remarks>
/// Like a manual "Run now", this starts the run directly rather than through trigger dispatch, so
/// trigger dispatch authorisers aren't consulted. They decide whether a service account may see a
/// trigger's output (content, media and similar); this trigger's output is only the caller's own
/// arguments. The executor still runs the automation as the workspace's service account.
/// </remarks>
internal sealed class AutomationMcpTool : McpServerTool
{
    private static readonly TimeSpan DefaultInitialPollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxPollInterval = TimeSpan.FromSeconds(2);

    private readonly Automation _automation;
    private readonly McpTriggerSettings _settings;
    private readonly IAutomationExecutor _executor;
    private readonly IAutomationRunService _runService;
    private readonly IRunDataSanitizer _runDataSanitizer;
    private readonly TimeSpan _initialPollInterval;

    public AutomationMcpTool(
        Automation automation,
        McpTriggerSettings settings,
        IAutomationExecutor executor,
        IAutomationRunService runService,
        IRunDataSanitizer runDataSanitizer,
        TimeSpan? initialPollInterval = null)
    {
        _automation = automation;
        _settings = settings;
        _executor = executor;
        _runService = runService;
        _runDataSanitizer = runDataSanitizer;
        _initialPollInterval = initialPollInterval ?? DefaultInitialPollInterval;

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

        Guid runId;
        try
        {
            // Not the request's token: once the run is saved it must go on to start and record its
            // workflow instance, even if the agent disconnects, or it's left looking stuck.
            runId = await _executor.ExecuteAsync(
                _automation,
                TriggerInitiatorType.AiAgent,
                initiatorId: null,
                triggerOutputData,
                CancellationToken.None);
        }
        catch (RateLimitExceededException)
        {
            return ErrorResult("Too many runs of this automation are in progress. Wait and try again.");
        }
        catch (AutomationEngineNotRunningException)
        {
            return ErrorResult("The automation engine isn't running on this server. Try again shortly.");
        }

        if (runId == Guid.Empty)
        {
            return ErrorResult("This automation has been auto-disabled by the circuit breaker. Re-enable it to run.");
        }

        var timeoutSeconds = Math.Clamp(_settings.TimeoutSeconds, 1, McpTriggerSettings.MaxTimeoutSeconds);
        var status = await WaitForTerminalStatusAsync(runId, TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        if (status is null || !IsTerminal(status.Value))
        {
            return new CallToolResult
            {
                IsError = false,
                Content = [new TextContentBlock
                {
                    Text = $"Still running after {timeoutSeconds}s. Run ID: {runId}.",
                }],
            };
        }

        // Load the full run once, now that it's finished, rather than on every poll.
        var run = await _runService.GetRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return ErrorResult($"Run {runId} could not be found.");
        }

        if (run.Status is AutomationRunStatus.Failed or AutomationRunStatus.Rejected or AutomationRunStatus.Cancelled)
        {
            return ErrorResult(run.Error ?? $"Run ended as {run.Status}.");
        }

        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = BuildResultText(run) }],
        };
    }

    /// <summary>
    /// Polls the run's status (not the whole run) until it finishes or the timeout passes, backing
    /// off from the initial interval to <see cref="MaxPollInterval"/> so a long wait doesn't query
    /// the database several times a second.
    /// </summary>
    private async Task<AutomationRunStatus?> WaitForTerminalStatusAsync(
        Guid runId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var interval = _initialPollInterval;

        while (true)
        {
            var status = await _runService.GetRunStatusAsync(runId, cancellationToken);
            if (status is not null && IsTerminal(status.Value))
            {
                return status;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                return status;
            }

            await Task.Delay(interval < remaining ? interval : remaining, cancellationToken);
            interval = TimeSpan.FromTicks(Math.Min(interval.Ticks * 2, MaxPollInterval.Ticks));
        }
    }

    private static bool IsTerminal(AutomationRunStatus status)
        => status is AutomationRunStatus.Completed or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled or AutomationRunStatus.Rejected;

    /// <summary>
    /// The output of the step that finished last, masked and truncated the way the backoffice shows
    /// run data: the caller may be unauthenticated, so sensitive values (tokens, cookies, fields the
    /// action marks sensitive) must not reach it, and a large output is cut to a readable size.
    /// </summary>
    /// <remarks>
    /// "Finished last" is by completion time, so in a flow with parallel branches it's the branch
    /// that ended the run, not the one that started last.
    /// </remarks>
    private string BuildResultText(AutomationRun run)
    {
        var lastOutput = run.StepRuns
            .Where(sr => sr.Status == StepRunStatus.Completed && sr.OutputData is not null)
            .OrderBy(sr => sr.CompletedUtc ?? sr.StartedUtc)
            .LastOrDefault();
        if (lastOutput is null)
        {
            return "Automation completed.";
        }

        var sanitized = _runDataSanitizer.SanitizeStepOutput(lastOutput.ActionAlias, lastOutput.OutputData);
        if (sanitized.Value is null)
        {
            return "Automation completed.";
        }

        return sanitized.Truncated
            ? $"{sanitized.Value}\n\n[Output truncated to {IRunDataSanitizer.MaxValueLength} characters.]"
            : sanitized.Value;
    }

    private static CallToolResult ErrorResult(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };
}
