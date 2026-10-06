using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Testing;

/// <summary>
/// Fluent test harness for executing actions in isolation.
/// Handles DI wiring, context construction, and connection mocking.
/// </summary>
/// <typeparam name="TAction">The action type to test.</typeparam>
public sealed class ActionTestHarness<TAction> where TAction : class, IAction
{
    private readonly ServiceCollection _services = new();
    private object? _settings;
    private readonly Dictionary<string, object?> _inputData = new();
    private ConfiguredConnection? _connection;
    private AutomationExecutionContext? _executionContext;
    private IReadOnlyDictionary<string, object?>? _bindingData;
    private Guid _automationId = Guid.NewGuid();
    private Guid _runId = Guid.NewGuid();
    private Guid _stepId = Guid.NewGuid();

    internal ActionTestHarness()
    {
        _services.AddSingleton<IEditableModelResolver>(new HarnessModelResolver(() => _settings));
        _services.AddSingleton<ActionInfrastructure>();
        _services.AddLogging(b => b.ClearProviders().AddProvider(NullLoggerProvider.Instance));
    }

    /// <summary>
    /// Registers a service instance for constructor injection into the action.
    /// </summary>
    public ActionTestHarness<TAction> WithService<TService>(TService instance) where TService : class
    {
        _services.AddSingleton(instance);
        return this;
    }

    /// <summary>
    /// Sets the action settings on the context.
    /// </summary>
    /// <remarks>
    /// When no <see cref="IEditableModelResolver"/> is registered with <see cref="WithService{TService}"/>, the
    /// harness's default resolver returns these settings for non-null data (previously it always returned null),
    /// so action code that resolves settings or injects the resolver now sees the configured settings.
    /// </remarks>
    public ActionTestHarness<TAction> WithSettings<TSettings>(TSettings settings) where TSettings : class
    {
        _settings = settings;
        return this;
    }

    /// <summary>
    /// Adds an input data entry to the context.
    /// </summary>
    public ActionTestHarness<TAction> WithInputData(string key, object? value)
    {
        _inputData[key] = value;
        return this;
    }

    /// <summary>
    /// Sets a configured connection on the context with the given typed settings.
    /// </summary>
    public ActionTestHarness<TAction> WithConnection<TConnectionSettings>(TConnectionSettings settings)
        where TConnectionSettings : class, new()
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Alias = "test-connection",
            Name = "Test Connection",
            Type = "test",
        };

        var connectionType = Mock.Of<IConnectionType>();
        _connection = new ConfiguredConnection(connection, connectionType, settings);
        return this;
    }

    /// <summary>
    /// Sets a configured connection on the context with the given type alias and settings.
    /// </summary>
    public ActionTestHarness<TAction> WithConnection<TConnectionSettings>(
        string typeAlias,
        TConnectionSettings settings) where TConnectionSettings : class, new()
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Alias = "test-connection",
            Name = "Test Connection",
            Type = typeAlias,
        };

        var connectionType = Mock.Of<IConnectionType>(ct => ct.Alias == typeAlias);
        _connection = new ConfiguredConnection(connection, connectionType, settings);
        return this;
    }

    /// <summary>
    /// Sets the execution context (service account identity, workspace info).
    /// </summary>
    public ActionTestHarness<TAction> WithExecutionContext(AutomationExecutionContext context)
    {
        _executionContext = context;
        return this;
    }

    /// <summary>
    /// Sets the binding data (trigger output + step outputs) on the context.
    /// </summary>
    public ActionTestHarness<TAction> WithBindingData(IReadOnlyDictionary<string, object?> data)
    {
        _bindingData = data;
        return this;
    }

    /// <summary>
    /// Overrides the default random automation ID.
    /// </summary>
    public ActionTestHarness<TAction> WithAutomationId(Guid id)
    {
        _automationId = id;
        return this;
    }

    /// <summary>
    /// Overrides the default random run ID.
    /// </summary>
    public ActionTestHarness<TAction> WithRunId(Guid id)
    {
        _runId = id;
        return this;
    }

    /// <summary>
    /// Overrides the default random step ID.
    /// </summary>
    public ActionTestHarness<TAction> WithStepId(Guid id)
    {
        _stepId = id;
        return this;
    }

    /// <summary>
    /// Builds the action via DI, constructs the context, and executes.
    /// </summary>
    public async Task<ActionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        using var sp = _services.BuildServiceProvider();
        return await ExecuteActionAsync(ActivatorUtilities.CreateInstance<TAction>(sp), cancellationToken);
    }

    /// <summary>
    /// Gets the outcomes the action declares for the settings given to
    /// <see cref="WithSettings{TSettings}"/>, resolved the way a run does: a static declaration is read as
    /// is, a dynamic one (<see cref="IStepType.HasDynamicOutcomes"/>) is resolved from the step's settings.
    /// </summary>
    /// <remarks>
    /// Unless an <see cref="IEditableModelResolver"/> was registered with <see cref="WithService{TService}"/>,
    /// the settings given to <see cref="WithSettings{TSettings}"/> are handed to the action as its typed
    /// settings. When no settings were given, a dynamic action sees null settings, exactly as at run time
    /// for a step with empty saved settings. Settings that serialise to an empty object are treated the same way.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The declared outcomes in declaration order, or an empty list when none are declared.</returns>
    public async Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(CancellationToken cancellationToken = default)
    {
        using var sp = _services.BuildServiceProvider();
        var action = ActivatorUtilities.CreateInstance<TAction>(sp);

        return action.HasDynamicOutcomes
            ? await action.GetOutcomesAsync(ToSavedSettings(), cancellationToken)
            : action.GetOutcomes();
    }

    /// <summary>
    /// Executes the action and works out which exit a run would follow, using the same rule as the
    /// run engine: the outcome the action returned, or its default outcome when it returned none.
    /// Dynamic outcomes are resolved from the settings given to <see cref="WithSettings{TSettings}"/>
    /// (see <see cref="GetOutcomesAsync"/>).
    /// </summary>
    /// <remarks>
    /// Problems are reported on the result rather than thrown, so a test can assert on them: when a
    /// run would fail the step (invalid declaration, or no outcome returned and no default declared)
    /// <see cref="RoutedActionResult.BranchOutcome"/> is null and
    /// <see cref="RoutedActionResult.OutcomeProblem"/> carries the message.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RoutedActionResult> ExecuteWithOutcomeAsync(CancellationToken cancellationToken = default)
    {
        using var sp = _services.BuildServiceProvider();
        var action = ActivatorUtilities.CreateInstance<TAction>(sp);
        var result = await ExecuteActionAsync(action, cancellationToken);

        // Only a plain success follows an exit; failures, skips and suspensions never reach routing.
        if (result.Status != ActionResultStatus.Success || result.Suspension is not null)
        {
            return new RoutedActionResult(result, null, null, null);
        }

        var savedSettings = action.HasDynamicOutcomes ? ToSavedSettings() : null;
        var routing = await StepOutcomeRouter.ResolveAsync(action, savedSettings, result.Outcome, cancellationToken);
        return new RoutedActionResult(result, routing.Outcome, routing.FailureMessage, routing.UndeclaredOutcomeWarning);
    }

    // Saved settings look like a camelCase JSON object; the step type only resolves them when non-empty.
    private Dictionary<string, object?>? ToSavedSettings()
    {
        if (_settings is null)
        {
            return null;
        }

        var element = JsonSerializer.SerializeToElement(_settings, JsonSerializerOptions.Web);
        return element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone())
            : null;
    }

    /// <summary>
    /// Default resolver, used when no <see cref="IEditableModelResolver"/> is registered with
    /// <see cref="WithService{TService}"/>. Returns null when <c>data</c> is null (the resolver contract);
    /// otherwise returns the object given to <c>WithSettings</c> when it fits the requested type.
    /// Behaviour change: this default previously returned null for every call (a mock), so an action
    /// that resolves its own settings via <c>ResolveSettings</c> now receives the <c>WithSettings</c> object.
    /// </summary>
    internal sealed class HarnessModelResolver(Func<object?> getSettings) : IEditableModelResolver
    {
        public TModel? ResolveModel<TModel>(string modelId, object? data, EditableModelSchema? schema = null)
            where TModel : class, new()
            => data is null ? null : getSettings() as TModel;

        public object? ResolveModel(string modelId, Type modelType, object? data, EditableModelSchema? schema = null)
            => data is not null && getSettings() is { } settings && modelType.IsInstanceOfType(settings) ? settings : null;
    }

    private async Task<ActionResult> ExecuteActionAsync(TAction action, CancellationToken cancellationToken)
    {
        var context = new ActionContext
        {
            AutomationId = _automationId,
            RunId = _runId,
            StepId = _stepId,
            ActionAlias = action.Alias,
            Settings = _settings,
            InputData = _inputData,
            Connection = _connection,
            ExecutionContext = _executionContext,
            BindingData = _bindingData,
            CancellationToken = cancellationToken,
            Action = action,
        };

        return await action.ExecuteAsync(context, cancellationToken);
    }
}

/// <summary>
/// Entry point for the action test harness.
/// </summary>
public static class ActionTestHarness
{
    /// <summary>
    /// Creates a test harness for the specified action type.
    /// </summary>
    public static ActionTestHarness<TAction> For<TAction>() where TAction : class, IAction
        => new();
}
