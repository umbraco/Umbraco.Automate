using Umbraco.Automate.Core.Bindings;

namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// Evaluates a <see cref="ConditionSet"/> against runtime binding data.
/// Uses <see cref="BindingEvaluator"/> to resolve operands before comparison.
/// </summary>
public sealed class ConditionEvaluator
{
    private readonly BindingEvaluator _bindingEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionEvaluator"/> class.
    /// </summary>
    public ConditionEvaluator(BindingEvaluator bindingEvaluator)
    {
        _bindingEvaluator = bindingEvaluator;
    }

    /// <summary>
    /// Evaluates a condition set: OR across groups, AND within each group.
    /// An empty set evaluates to true (vacuously true — no conditions means pass).
    /// </summary>
    /// <param name="conditionSet">The condition set to evaluate.</param>
    /// <param name="bindingData">The runtime data for resolving <c>${ }</c> bindings in operands.</param>
    /// <returns>True if at least one group passes (all its conditions are true).</returns>
    public bool Evaluate(ConditionSet conditionSet, IReadOnlyDictionary<string, object?> bindingData)
    {
        if (conditionSet.Groups.Count == 0)
        {
            return true;
        }

        // OR across groups: at least one group must pass.
        foreach (var group in conditionSet.Groups)
        {
            if (EvaluateGroup(group, bindingData))
            {
                return true;
            }
        }

        return false;
    }

    private bool EvaluateGroup(ConditionGroup group, IReadOnlyDictionary<string, object?> bindingData)
    {
        if (group.Conditions.Count == 0)
        {
            return true;
        }

        // AND within group: all conditions must pass.
        foreach (var condition in group.Conditions)
        {
            var left = _bindingEvaluator.Evaluate(condition.LeftOperand, bindingData);
            var right = _bindingEvaluator.Evaluate(condition.RightOperand, bindingData);

            if (!EvaluateCondition(left, condition.Operator, right))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvaluateCondition(string left, ConditionOperator op, string right)
    {
        return op switch
        {
            ConditionOperator.Equals => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotContains => !left.Contains(right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.StartsWith => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.EndsWith => left.EndsWith(right, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.GreaterThan => CompareNumeric(left, right) > 0,
            ConditionOperator.LessThan => CompareNumeric(left, right) < 0,
            ConditionOperator.GreaterThanOrEquals => CompareNumeric(left, right) >= 0,
            ConditionOperator.LessThanOrEquals => CompareNumeric(left, right) <= 0,
            ConditionOperator.IsEmpty => string.IsNullOrEmpty(left),
            ConditionOperator.IsNotEmpty => !string.IsNullOrEmpty(left),
            _ => false,
        };
    }

    /// <summary>
    /// Attempts numeric comparison; falls back to ordinal string comparison.
    /// </summary>
    private static int CompareNumeric(string left, string right)
    {
        if (decimal.TryParse(left, out var leftNum) && decimal.TryParse(right, out var rightNum))
        {
            return leftNum.CompareTo(rightNum);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
