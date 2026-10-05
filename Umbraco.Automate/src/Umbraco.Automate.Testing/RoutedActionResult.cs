using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Testing;

/// <summary>
/// The result of executing an action through <see cref="ActionTestHarness{TAction}"/> together with
/// the exit a run would follow, worked out by the same rule the run engine applies.
/// </summary>
/// <remarks>
/// When <see cref="OutcomeProblem"/> is set, <see cref="Result"/> is left exactly as the action returned it
/// (its status may still be Success), whereas a real run records a Validation failure for the step. Assert on
/// <see cref="OutcomeProblem"/> and <see cref="BranchOutcome"/>, not on <c>Result.Status</c>.
/// </remarks>
/// <param name="Result">What the action returned.</param>
/// <param name="BranchOutcome">
/// The effective branch outcome: the outcome the action returned, or its default outcome when it
/// returned none. Null when the action declares no outcomes and returned none, when the result was
/// not a plain success, or when <see cref="OutcomeProblem"/> is set.
/// </param>
/// <param name="OutcomeProblem">
/// Set when a run would fail the step because of the outcome (the declaration is invalid, or the
/// action returned none and declares no default). Null otherwise.
/// </param>
/// <param name="UndeclaredOutcomeWarning">
/// Set when the action returned an outcome it does not declare. A run still routes on it but warns.
/// </param>
public sealed record RoutedActionResult(
    ActionResult Result,
    string? BranchOutcome,
    string? OutcomeProblem,
    string? UndeclaredOutcomeWarning);
