using Shouldly;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Expressions;

namespace Umbraco.Automate.Tests.Unit.Conditions;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator = new(
        new ExpressionEvaluator(new ExpressionFilterCollection(Array.Empty<IExpressionFilter>)));

    private static readonly Dictionary<string, object?> EmptyData = new();

    #region Single Condition — Each Operator

    [Fact]
    public void Equals_MatchingValues_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.Equals, "hello");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void Equals_CaseInsensitive()
    {
        var set = BuildSingleCondition("Hello", ConditionOperator.Equals, "hello");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.Equals, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    [Fact]
    public void NotEquals_DifferentValues_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.NotEquals, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void NotEquals_MatchingValues_ReturnsFalse()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.NotEquals, "hello");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    [Fact]
    public void Contains_Substring_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello world", ConditionOperator.Contains, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void Contains_NoSubstring_ReturnsFalse()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.Contains, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    [Fact]
    public void NotContains_NoSubstring_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.NotContains, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_MatchingPrefix_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello world", ConditionOperator.StartsWith, "hello");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_MatchingSuffix_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello world", ConditionOperator.EndsWith, "world");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThan_NumericComparison_ReturnsTrue()
    {
        var set = BuildSingleCondition("10", ConditionOperator.GreaterThan, "5");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThan_EqualValues_ReturnsFalse()
    {
        var set = BuildSingleCondition("5", ConditionOperator.GreaterThan, "5");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    [Fact]
    public void LessThan_NumericComparison_ReturnsTrue()
    {
        var set = BuildSingleCondition("3", ConditionOperator.LessThan, "10");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void GreaterThanOrEquals_EqualValues_ReturnsTrue()
    {
        var set = BuildSingleCondition("5", ConditionOperator.GreaterThanOrEquals, "5");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void LessThanOrEquals_LesserValue_ReturnsTrue()
    {
        var set = BuildSingleCondition("3", ConditionOperator.LessThanOrEquals, "5");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_EmptyString_ReturnsTrue()
    {
        var set = BuildSingleCondition("", ConditionOperator.IsEmpty, "ignored");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void IsEmpty_NonEmptyString_ReturnsFalse()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.IsEmpty, "ignored");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    [Fact]
    public void IsNotEmpty_NonEmptyString_ReturnsTrue()
    {
        var set = BuildSingleCondition("hello", ConditionOperator.IsNotEmpty, "ignored");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void IsNotEmpty_EmptyString_ReturnsFalse()
    {
        var set = BuildSingleCondition("", ConditionOperator.IsNotEmpty, "ignored");
        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    #endregion

    #region AND Logic

    [Fact]
    public void And_AllConditionsTrue_ReturnsTrue()
    {
        var set = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions =
                    [
                        new Condition { LeftOperand = "hello", Operator = ConditionOperator.Equals, RightOperand = "hello" },
                        new Condition { LeftOperand = "10", Operator = ConditionOperator.GreaterThan, RightOperand = "5" },
                    ],
                },
            ],
        };

        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void And_OneConditionFalse_ReturnsFalse()
    {
        var set = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions =
                    [
                        new Condition { LeftOperand = "hello", Operator = ConditionOperator.Equals, RightOperand = "hello" },
                        new Condition { LeftOperand = "3", Operator = ConditionOperator.GreaterThan, RightOperand = "5" },
                    ],
                },
            ],
        };

        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    #endregion

    #region OR Logic

    [Fact]
    public void Or_FirstGroupTrue_ReturnsTrue()
    {
        var set = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }],
                },
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "b" }],
                },
            ],
        };

        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void Or_SecondGroupTrue_ReturnsTrue()
    {
        var set = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "b" }],
                },
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }],
                },
            ],
        };

        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void Or_AllGroupsFalse_ReturnsFalse()
    {
        var set = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "b" }],
                },
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = "c", Operator = ConditionOperator.Equals, RightOperand = "d" }],
                },
            ],
        };

        _evaluator.Evaluate(set, EmptyData).ShouldBeFalse();
    }

    #endregion

    #region Expression Resolution

    [Fact]
    public void ExpressionResolution_ResolvesOperands()
    {
        var data = new Dictionary<string, object?>
        {
            ["trigger"] = new Dictionary<string, object?> { ["status"] = "published" },
        };

        var set = BuildSingleCondition("${ trigger.status }", ConditionOperator.Equals, "published");
        _evaluator.Evaluate(set, data).ShouldBeTrue();
    }

    [Fact]
    public void ExpressionResolution_BothOperands()
    {
        var data = new Dictionary<string, object?>
        {
            ["trigger"] = new Dictionary<string, object?> { ["a"] = "same", ["b"] = "same" },
        };

        var set = BuildSingleCondition("${ trigger.a }", ConditionOperator.Equals, "${ trigger.b }");
        _evaluator.Evaluate(set, data).ShouldBeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EmptyConditionSet_ReturnsTrue()
    {
        var set = new ConditionSet();
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void EmptyGroup_ReturnsTrue()
    {
        var set = new ConditionSet { Groups = [new ConditionGroup()] };
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue();
    }

    [Fact]
    public void NumericComparisonFallback_NonNumericStrings()
    {
        // Falls back to string comparison when not numeric
        var set = BuildSingleCondition("banana", ConditionOperator.GreaterThan, "apple");
        _evaluator.Evaluate(set, EmptyData).ShouldBeTrue(); // "banana" > "apple" lexically
    }

    #endregion

    private static ConditionSet BuildSingleCondition(string left, ConditionOperator op, string right)
    {
        return new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = left, Operator = op, RightOperand = right }],
                },
            ],
        };
    }
}
