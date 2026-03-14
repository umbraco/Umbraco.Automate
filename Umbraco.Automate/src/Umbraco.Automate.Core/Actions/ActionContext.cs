using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Provides runtime context to an action during execution.
/// </summary>
public sealed class ActionContext
{
    /// <summary>
    /// Gets the ID of the automation being executed.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the ID of the current run.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the ID of the step being executed.
    /// </summary>
    public required Guid StepId { get; init; }

    /// <summary>
    /// Gets the action alias.
    /// </summary>
    public required string ActionAlias { get; init; }

    /// <summary>
    /// Gets the deserialized settings for this step, or null if the action has no settings.
    /// </summary>
    public object? Settings { get; init; }

    /// <summary>
    /// Gets the resolved input data for this step (expression values already evaluated).
    /// </summary>
    public IReadOnlyDictionary<string, object?> InputData { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets the settings as the expected type.
    /// </summary>
    /// <typeparam name="T">The settings POCO type.</typeparam>
    /// <returns>The typed settings, or a default instance if settings are null.</returns>
    public T GetSettings<T>() where T : class, new()
        => Settings as T ?? new T();

    /// <summary>
    /// Gets a cancellation token linked to the step timeout.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the execution context carrying the service account identity and workspace info.
    /// Available for CMS-modifying actions that need to execute as the workspace's service account.
    /// </summary>
    public AutomationExecutionContext? ExecutionContext { get; init; }

    /// <summary>
    /// Gets the configured connection for this step, or null if no connection is configured.
    /// Contains the resolved connection type and decrypted settings, ready for runtime use.
    /// </summary>
    public ConfiguredConnection? Connection { get; init; }

    /// <summary>
    /// Gets the action instance being executed. Set by the pipeline for middleware inspection.
    /// </summary>
    public IAction? Action { get; init; }

    /// <summary>
    /// Gets the runtime binding data (trigger output + step outputs) for actions that
    /// need to evaluate bindings directly (e.g. condition evaluation in the If action).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? BindingData { get; init; }
}
