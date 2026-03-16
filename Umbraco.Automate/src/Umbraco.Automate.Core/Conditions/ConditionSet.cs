namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// A set of condition groups joined by OR. At least one group must pass for the set to evaluate to true.
/// Follows DNF (Disjunctive Normal Form): (A AND B) OR (C AND D).
/// </summary>
public sealed class ConditionSet
{
    /// <summary>
    /// Gets or sets the condition groups.
    /// </summary>
    public List<ConditionGroup> Groups { get; set; } = [];
}
