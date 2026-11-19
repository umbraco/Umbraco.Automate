namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Works out which exit a successful step result follows. Shared by the run-time step body and the
/// action test harness so both apply the same rule.
/// </summary>
internal static class StepOutcomeRouter
{
    /// <summary>
    /// Reads the step type's declared outcomes the same way the publish check does: a static
    /// declaration is read without touching settings, a dynamic one is resolved from the step's saved,
    /// unbound settings. Nothing is enforced for a step type that declares nothing.
    /// </summary>
    /// <param name="stepType">The step type that produced the result.</param>
    /// <param name="savedSettings">The step's saved, unbound settings. Only read for dynamic declarations.</param>
    /// <param name="returnedOutcome">The outcome the step type returned, or null if it named none.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<StepOutcomeRouting> ResolveAsync(
        IStepType stepType,
        Dictionary<string, object?>? savedSettings,
        string? returnedOutcome,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        IReadOnlyList<StepOutcome>? declared;
        try
        {
            declared = stepType.HasDynamicOutcomes
                ? await stepType.GetOutcomesAsync(savedSettings, cancellationToken)
                : stepType.GetOutcomes();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return StepOutcomeRouting.Failure($"Action '{stepType.Alias}' could not list its outcomes: {ex.Message}", ex);
        }

        if (declared is null)
        {
            return StepOutcomeRouting.Failure($"Action '{stepType.Alias}' could not list its outcomes: the action returned no list.");
        }

        // Nothing declared: route on whatever was returned.
        if (!stepType.HasDynamicOutcomes && declared.Count == 0)
        {
            return StepOutcomeRouting.Route(returnedOutcome);
        }

        var declarationErrors = StepOutcomeValidator.Validate(declared);
        if (declarationErrors.Count > 0)
        {
            return StepOutcomeRouting.Failure(
                $"Action '{stepType.Alias}' declares invalid outcomes: {string.Join(" ", declarationErrors)}");
        }

        if (returnedOutcome is null)
        {
            var defaultOutcome = declared.FirstOrDefault(o => o.IsDefault);
            return defaultOutcome is null
                ? StepOutcomeRouting.Failure($"Action '{stepType.Alias}' must return one of its declared outcomes.")
                : StepOutcomeRouting.Route(defaultOutcome.Key);
        }

        // An undeclared key still routes (an "Any result" line fires; named exits do not), but is
        // flagged so the author can see the action and its declaration disagree.
        return declared.Any(o => string.Equals(o.Key, returnedOutcome, StringComparison.Ordinal))
            ? StepOutcomeRouting.Route(returnedOutcome)
            : StepOutcomeRouting.Route(
                returnedOutcome,
                $"Action returned outcome '{returnedOutcome}', which it does not declare.");
    }
}

/// <summary>
/// The exit a successful step result follows, or why it can't follow one.
/// </summary>
/// <param name="Outcome">The outcome to route on. Null when none applies or on failure.</param>
/// <param name="FailureMessage">Why the step must fail, or null when routing succeeded.</param>
/// <param name="UndeclaredOutcomeWarning">Set when the returned outcome is routed but not declared.</param>
/// <param name="Exception">The exception thrown while listing outcomes, if that caused the failure.</param>
internal readonly record struct StepOutcomeRouting(
    string? Outcome,
    string? FailureMessage,
    string? UndeclaredOutcomeWarning,
    Exception? Exception = null)
{
    internal static StepOutcomeRouting Route(string? outcome, string? undeclaredOutcomeWarning = null)
        => new(outcome, null, undeclaredOutcomeWarning);

    internal static StepOutcomeRouting Failure(string message, Exception? exception = null)
        => new(null, message, null, exception);
}
