using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Decides whether a step failure should be treated as terminal (no retry) or transient
/// (retry permitted by WorkflowCore). Invoked by <see cref="ActionStepBody"/> whenever
/// an action execution — or the pre-execution setup phase — produces a failure.
/// </summary>
/// <remarks>
/// The default implementation (<see cref="DefaultStepErrorClassifier"/>) is intentionally
/// narrow. Replace the DI registration with a custom implementation to tune retry policy
/// (e.g. allow a single retry for authentication failures to absorb token-refresh races).
/// </remarks>
public interface IStepErrorClassifier
{
    /// <summary>
    /// Returns <c>true</c> if a failure with the given error category should be treated
    /// as terminal for the current attempt — i.e. the step should not be retried, and
    /// the workflow should either skip or terminate depending on the step's configured
    /// <c>ErrorBehavior</c>.
    /// </summary>
    /// <param name="category">The category of the failure. Unknown/unset is treated as transient.</param>
    bool IsTerminal(StepRunErrorCategory category);

    /// <summary>
    /// Classifies an exception raised during step setup (before the action middleware
    /// pipeline can intercept it) into a <see cref="StepRunErrorCategory"/>.
    /// </summary>
    /// <param name="exception">The exception thrown during step setup.</param>
    StepRunErrorCategory Classify(Exception exception);
}
