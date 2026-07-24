using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Scripting;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that runs a user-authored JavaScript function against the step inputs and
/// returns its result. The script is executed in a sandboxed Jint engine via <see cref="ScriptExecutor"/>.
/// </summary>
[Action("umbracoAutomate.runScript", "Run Script",
    Description = "Runs a JavaScript function against the step inputs and returns its result.",
    Group = "Core",
    Icon = "icon-script")]
public sealed class RunScriptAction : ActionBase<RunScriptSettings, RunScriptOutput>, IValidatableStepType
{
    private readonly ScriptExecutor _executor;
    private readonly IScriptValidator _validator;
    private readonly IOptions<ScriptingOptions> _scriptingOptions;
    private readonly IOptions<ExecutionOptions> _executionOptions;
    private readonly ILogger<RunScriptAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunScriptAction"/> class.
    /// </summary>
    public RunScriptAction(
        ActionInfrastructure infrastructure,
        ScriptExecutor executor,
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
    public IReadOnlyList<string> ValidateSettings(object? settings)
        => settings is RunScriptSettings s ? _validator.Validate(s.Script) : [];

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

        var data = JsonSerializer.SerializeToNode(context.InputData);

        // Cap the script's total runtime at the smaller of the configured scripting timeout and
        // the step's own timeout budget, so a script can never outlive its step.
        var totalTimeout = scripting.TotalExecutionTimeout < _executionOptions.Value.DefaultTimeout
            ? scripting.TotalExecutionTimeout
            : _executionOptions.Value.DefaultTimeout;

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
            OnLogMessage = message => WriteLog(context, message),
        };

        var result = await _executor.ExecuteAsync("script", settings.Script, data, options, cancellationToken);

        if (error is { } err)
        {
            return ActionResult.Failed(new InvalidOperationException(err.Message), MapCategory(err.Kind));
        }

        return Success(new RunScriptOutput { Result = result });
    }

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

    private static StepRunErrorCategory MapCategory(ScriptErrorKind kind)
        => kind switch
        {
            ScriptErrorKind.Compilation => StepRunErrorCategory.Validation,
            ScriptErrorKind.Timeout => StepRunErrorCategory.Timeout,
            _ => StepRunErrorCategory.Unknown,
        };
}
