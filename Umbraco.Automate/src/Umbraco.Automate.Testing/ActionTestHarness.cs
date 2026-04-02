using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;

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
        _services.AddSingleton(Mock.Of<IEditableModelResolver>());
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
        var action = ActivatorUtilities.CreateInstance<TAction>(sp);

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
