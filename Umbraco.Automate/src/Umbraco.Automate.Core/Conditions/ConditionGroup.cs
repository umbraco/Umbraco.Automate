namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// A group of conditions joined by AND. All conditions must be true for the group to pass.
/// </summary>
public sealed class ConditionGroup
{
    /// <summary>
    /// Gets or sets the conditions in this group.
    /// </summary>
    public List<Condition> Conditions { get; set; } = [];
}
