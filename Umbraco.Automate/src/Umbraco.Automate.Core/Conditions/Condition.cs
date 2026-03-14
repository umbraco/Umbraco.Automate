namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// A single condition comparing a left operand against a right operand using an operator.
/// Operands support <c>${ expression }</c> syntax for runtime resolution.
/// </summary>
public sealed class Condition
{
    /// <summary>
    /// Gets or sets the left operand (the value to test).
    /// </summary>
    public string LeftOperand { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comparison operator.
    /// </summary>
    public ConditionOperator Operator { get; set; }

    /// <summary>
    /// Gets or sets the right operand (the value to compare against).
    /// Ignored for <see cref="ConditionOperator.IsEmpty"/> and <see cref="ConditionOperator.IsNotEmpty"/>.
    /// </summary>
    public string RightOperand { get; set; } = string.Empty;
}
