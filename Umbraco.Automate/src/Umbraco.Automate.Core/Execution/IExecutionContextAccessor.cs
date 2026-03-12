namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Provides access to the current automation execution context.
/// Uses <c>AsyncLocal</c> storage — the context is scoped to the executing async flow.
/// </summary>
public interface IExecutionContextAccessor
{
    /// <summary>
    /// Gets the current execution context, or <c>null</c> if not executing within an automation.
    /// </summary>
    AutomationExecutionContext? ExecutionContext { get; }
}
