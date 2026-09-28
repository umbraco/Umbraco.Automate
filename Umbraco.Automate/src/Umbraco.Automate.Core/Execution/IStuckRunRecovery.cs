namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Fails the automation runs a previous process left in flight, and stops the engine from
/// resuming them. Called by <see cref="WorkflowHostLifecycle"/> before the WorkflowCore host
/// starts, so an interrupted step is never executed again behind a run the UI reports as failed.
/// </summary>
internal interface IStuckRunRecovery
{
    /// <summary>
    /// Marks runs left in Running/Pending as Failed and terminates their workflow instances.
    /// </summary>
    Task RecoverStuckRunsAsync(CancellationToken cancellationToken);
}
