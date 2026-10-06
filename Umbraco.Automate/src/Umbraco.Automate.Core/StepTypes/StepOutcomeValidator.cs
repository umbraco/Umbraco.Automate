namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Checks that a step type's declared outcomes follow the declaration rules.
/// </summary>
/// <remarks>
/// Keys are compared ordinally and case-sensitively because they are stored verbatim on connections.
/// Keys starting with <see cref="ReservedKeyPrefix"/> are reserved for system exits.
/// </remarks>
internal static class StepOutcomeValidator
{
    /// <summary>
    /// The prefix reserved for system-defined exits (for example "any result").
    /// </summary>
    internal const string ReservedKeyPrefix = "__";

    /// <summary>
    /// Validates a declaration and returns every violation found.
    /// </summary>
    /// <param name="outcomes">The declared outcomes. An empty declaration is valid.</param>
    /// <returns>A message per violation. Empty when the declaration is valid.</returns>
    internal static IReadOnlyList<string> Validate(IReadOnlyList<StepOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);

        var errors = new List<string>();
        var present = new List<StepOutcome>(outcomes.Count);

        // Third-party declarations can contain null items despite the type, so report them instead of throwing.
        for (var i = 0; i < outcomes.Count; i++)
        {
            var position = i + 1;
            if (outcomes[i] is not { } outcome)
            {
                errors.Add($"Outcome #{position} is null.");
            }
            else if (string.IsNullOrWhiteSpace(outcome.Key))
            {
                errors.Add($"Outcome #{position} has an empty key.");
            }
            else
            {
                present.Add(outcome);
            }
        }

        // Only outcomes with a usable key reach the key checks below.
        var namedKeys = present.Select(o => o.Key).ToList();

        errors.AddRange(namedKeys
            .GroupBy(k => k, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"Outcome key '{g.Key}' is declared more than once."));

        errors.AddRange(namedKeys
            .Where(k => k.StartsWith(ReservedKeyPrefix, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Select(k => $"Outcome keys can't start with '{ReservedKeyPrefix}': '{k}'."));

        var defaults = outcomes.Where(o => o is { IsDefault: true }).ToList();
        if (defaults.Count > 1)
        {
            var names = string.Join(", ", defaults.Select(o => $"'{o.Key}'"));
            errors.Add($"At most one outcome can be the default; found {defaults.Count}: {names}.");
        }

        return errors;
    }
}
