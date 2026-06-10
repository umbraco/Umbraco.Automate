namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// <c>AsyncLocal</c>-based implementation of <see cref="IExecutionContextAccessor"/>.
/// The executor sets the context before starting a workflow; actions read it during execution.
/// </summary>
internal sealed class ExecutionContextAccessor : IExecutionContextAccessor
{
    private static readonly AsyncLocal<AutomationExecutionContext?> Current = new();

    public AutomationExecutionContext? ExecutionContext => Current.Value;

    /// <summary>
    /// Sets the execution context for the current async flow. Returns an <see cref="IDisposable"/>
    /// that clears the context when disposed.
    /// </summary>
    internal static IDisposable Set(AutomationExecutionContext context)
    {
        Current.Value = context;
        return new ContextScope();
    }

    private sealed class ContextScope : IDisposable
    {
        public void Dispose() => Current.Value = null;
    }
}
