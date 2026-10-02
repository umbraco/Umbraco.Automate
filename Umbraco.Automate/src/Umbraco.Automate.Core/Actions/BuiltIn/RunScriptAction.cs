using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Scripting;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that runs a user-authored JavaScript function against the step's binding
/// context (trigger output, prior step outputs, loop item) and returns its result. The script is executed in a sandboxed Jint engine via <see cref="IScriptExecutor"/>.
/// </summary>
[Action("umbracoAutomate.runScript", "Run Script",
    Description = "Runs a JavaScript function against the trigger and previous step outputs and returns its result.",
    Group = "Core",
    Icon = "icon-script")]
public sealed class RunScriptAction : ActionBase<RunScriptSettings, RunScriptOutput>, IValidatableStepType
{
    /// <summary>
    /// The reserved output property that always holds the script's return value — the camel-cased
    /// name of <see cref="RunScriptOutput.Result"/> as it appears in binding expressions.
    /// </summary>
    public const string ResultPropertyName = "result";

    private readonly IScriptExecutor _executor;
    private readonly IScriptValidator _validator;
    private readonly IOptions<ScriptingOptions> _scriptingOptions;
    private readonly IOptions<ExecutionOptions> _executionOptions;
    private readonly ILogger<RunScriptAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunScriptAction"/> class.
    /// </summary>
    public RunScriptAction(
        ActionInfrastructure infrastructure,
        IScriptExecutor executor,
        IScriptValidator validator,
        IOptions<ScriptingOptions> scriptingOptions,
        IOptions<ExecutionOptions> executionOptions,
        ILogger<RunScriptAction> logger)
        : base(infrastructure)
    {
        _executor = executor;
        _validator = validator;
        _scriptingOptions = scriptingOptions;
        _executionOptions = executionOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ValidateSettingsAsync(object? settings, CancellationToken cancellationToken = default)
    {
        if (settings is not RunScriptSettings s)
        {
            return [];
        }

        var errors = new List<string>(await _validator.ValidateScriptAsync(s.Script, cancellationToken));

        // A schema that cannot be used only degrades binding autocomplete at design time, so the
        // save is where the author gets told about it.
        _ = TryParseOutputSchema(s.OutputSchema, out var schemaError);
        if (schemaError is not null)
        {
            errors.Add(schemaError);
        }

        return errors;
    }

    /// <inheritdoc />
    public override bool HasDynamicOutputSchema => true;

    /// <summary>
    /// Resolves the output schema from the configured <see cref="RunScriptSettings.OutputSchema"/>,
    /// so the binding UI can offer the individual properties the script returns rather than an
    /// opaque <c>result</c>. Falls back to the static schema when none is configured — the
    /// reserved <c>result</c> property stays bindable either way.
    /// </summary>
    protected override Task<JsonSchema?> GetOutputSchemaAsync(
        RunScriptSettings? settings,
        CancellationToken cancellationToken = default)
    {
        var declared = settings is null ? null : TryParseOutputSchema(settings.OutputSchema, out _);
        if (declared is null)
        {
            return Task.FromResult(GetOutputSchema());
        }

        // The configured schema describes what the script returns, which the action always
        // publishes under the reserved `result` property — a script may return an array or a
        // primitive, which has nowhere to go if flattened onto the output root.
        var wrapped = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { [ResultPropertyName] = declared },
        };

        return Task.FromResult<JsonSchema?>(JsonSchema.FromText(wrapped.ToJsonString()));
    }

    /// <summary>
    /// Parses the configured output schema. Returns <c>null</c> when there is none or it cannot be
    /// used, with <paramref name="error"/> describing why in the latter case.
    /// </summary>
    private static JsonNode? TryParseOutputSchema(string? json, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            error = $"Output schema is not valid JSON: {ex.Message}";
            return null;
        }

        if (node is not JsonObject)
        {
            error = "Output schema must be a JSON object describing a JSON Schema.";
            return null;
        }

        try
        {
            JsonSchema.FromText(json);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            error = $"Output schema is not a valid JSON Schema: {ex.Message}";
            return null;
        }

        return node;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var scripting = _scriptingOptions.Value;

        if (!scripting.Enabled)
        {
            return ActionResult.Failed(
                new InvalidOperationException(
                    "The Run Script action is disabled. Enable it via 'Umbraco:Automate:Scripting:Enabled'."),
                StepRunErrorCategory.ConfigurationError);
        }

        var settings = context.GetSettings<RunScriptSettings>();

        if (string.IsNullOrWhiteSpace(settings.Script))
        {
            return ActionResult.Failed(
                new ArgumentException("Script is required."),
                StepRunErrorCategory.Validation);
        }

        JsonObject data;
        try
        {
            data = RunScriptData.Build(context);
        }
        catch (JsonException ex)
        {
            return ActionResult.Failed(new InvalidOperationException(ex.Message, ex), StepRunErrorCategory.Validation);
        }

        // Cap the script's total runtime at the smaller of the configured scripting timeout and
        // the step's own timeout budget, so a script can never outlive its step.
        var totalTimeout = scripting.TotalExecutionTimeout < _executionOptions.Value.DefaultTimeout
            ? scripting.TotalExecutionTimeout
            : _executionOptions.Value.DefaultTimeout;

        if (settings.AllowFetch && !scripting.FetchEnabled)
        {
            context.LogWarning("fetch() is not available because it is disabled by 'Umbraco:Automate:Scripting:FetchEnabled'");
        }

        // The script runs on a worker thread, and one abandoned at its time budget can keep
        // calling console after this method has returned, so run-log writes are serialised and
        // stop once the executor hands back control.
        var runLogLock = new object();
        var runLogOpen = true;

        ScriptError? error = null;
        var options = new ScriptExecutorOptions
        {
            // fetch requires both the tenant-wide master switch and the per-step toggle.
            AllowFetch = scripting.FetchEnabled && settings.AllowFetch,
            FetchAllowedHosts = scripting.FetchAllowedHosts,
            MaxMemoryBytes = scripting.MaxMemoryBytes,
            MaxRecursionDepth = scripting.MaxRecursionDepth,
            MaxArraySize = scripting.MaxArraySize,
            MaxStatements = scripting.MaxStatements,
            StatementTimeout = scripting.StatementTimeout,
            TotalExecutionTimeout = totalTimeout,
            HttpRequestTimeout = scripting.HttpRequestTimeout,
            MaxResponseBodyBytes = scripting.MaxResponseBodyBytes,
            OnError = e => error = e,
            OnLogMessage = message =>
            {
                WriteLog(context, message);

                lock (runLogLock)
                {
                    if (runLogOpen)
                    {
                        context.Log(MapActionLogLevel(message.Level), message.Message);
                    }
                }
            },
        };

        var started = Stopwatch.GetTimestamp();
        var result = await _executor.ExecuteAsync("script", settings.Script, data, options, cancellationToken);
        var elapsed = ActionLogFormat.Elapsed(Stopwatch.GetElapsedTime(started));

        lock (runLogLock)
        {
            runLogOpen = false;
        }

        if (error is { } err)
        {
            context.LogError(DescribeError(err, elapsed));
            return ActionResult.Failed(new InvalidOperationException(err.Message), MapCategory(err.Kind));
        }

        context.LogInfo($"Script completed in {elapsed}");

        return Success(new RunScriptOutput { Result = result });
    }

    private static string DescribeError(ScriptError error, string elapsed)
        => error.Kind switch
        {
            ScriptErrorKind.Compilation => $"Script could not be compiled: {error.Message}",
            ScriptErrorKind.Timeout => $"Script stopped after {elapsed} for exceeding its time or resource limits: {error.Message}",
            ScriptErrorKind.Runtime => $"Script threw an error after {elapsed}: {error.Message}",
            _ => $"Script failed after {elapsed}: {error.Message}",
        };

    private void WriteLog(ActionContext context, LogMessage message) =>
        _logger.Log(
            MapLogLevel(message.Level),
            "Automation {AutomationId} / Run {RunId} / Step {StepId} [script]: {Message}",
            context.AutomationId, context.RunId, context.StepId, message.Message);

    private static LogLevel MapLogLevel(string level)
        => level switch
        {
            "error" => LogLevel.Error,
            "warn" => LogLevel.Warning,
            "debug" => LogLevel.Debug,
            "trace" => LogLevel.Trace,
            _ => LogLevel.Information,
        };

    private static ActionLogLevel MapActionLogLevel(string level)
        => level switch
        {
            "error" => ActionLogLevel.Error,
            "warn" => ActionLogLevel.Warning,
            "debug" or "trace" => ActionLogLevel.Debug,
            _ => ActionLogLevel.Info,
        };

    private static StepRunErrorCategory MapCategory(ScriptErrorKind kind)
        => kind switch
        {
            ScriptErrorKind.Compilation => StepRunErrorCategory.Validation,
            ScriptErrorKind.Timeout => StepRunErrorCategory.Timeout,
            _ => StepRunErrorCategory.Unknown,
        };
}
