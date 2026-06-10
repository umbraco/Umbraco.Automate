namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// A named case in a Switch action. Each case has a name (the outcome handle)
/// and a <see cref="ConditionSet"/> that determines when the case matches.
/// Cases are evaluated in order; the first match wins.
/// </summary>
public sealed class SwitchCase
{
    /// <summary>
    /// Gets or sets the case name, used as the outcome handle for connection wiring.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the conditions that must be satisfied for this case to match.
    /// An empty condition set is vacuously true (always matches — use as a fallback).
    /// </summary>
    public ConditionSet Conditions { get; set; } = new();
}
