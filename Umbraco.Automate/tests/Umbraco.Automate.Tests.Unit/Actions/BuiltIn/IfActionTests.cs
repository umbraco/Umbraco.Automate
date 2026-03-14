using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class IfActionTests
{
    private readonly IfAction _action;

    public IfActionTests()
    {
        var evaluator = new ExpressionEvaluator(new ExpressionFilterCollection(Array.Empty<IExpressionFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);
        _action = new IfAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            conditionEvaluator);
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.if");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("If");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(IfActionSettings));

    [Fact]
    public async Task ExecuteAsync_ConditionsTrue_ReturnsTrueOutcome()
    {
        var settings = new IfActionSettings
        {
            Conditions = new ConditionSet
            {
                Groups =
                [
                    new ConditionGroup
                    {
                        Conditions = [new Condition { LeftOperand = "yes", Operator = ConditionOperator.Equals, RightOperand = "yes" }],
                    },
                ],
            },
        };

        var context = CreateContext(settings);
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("true");
    }

    [Fact]
    public async Task ExecuteAsync_ConditionsFalse_ReturnsFalseOutcome()
    {
        var settings = new IfActionSettings
        {
            Conditions = new ConditionSet
            {
                Groups =
                [
                    new ConditionGroup
                    {
                        Conditions = [new Condition { LeftOperand = "yes", Operator = ConditionOperator.Equals, RightOperand = "no" }],
                    },
                ],
            },
        };

        var context = CreateContext(settings);
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("false");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyConditionSet_ReturnsTrueOutcome()
    {
        var settings = new IfActionSettings { Conditions = new ConditionSet() };

        var context = CreateContext(settings);
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("true");
    }

    [Fact]
    public async Task ExecuteAsync_OutputIncludesResultBoolean()
    {
        var settings = new IfActionSettings
        {
            Conditions = new ConditionSet
            {
                Groups =
                [
                    new ConditionGroup
                    {
                        Conditions = [new Condition { LeftOperand = "a", Operator = ConditionOperator.Equals, RightOperand = "a" }],
                    },
                ],
            },
        };

        var context = CreateContext(settings);
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.OutputData.ShouldNotBeNull();
    }

    private static ActionContext CreateContext(IfActionSettings settings) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.if",
        Settings = settings,
        ExpressionData = new Dictionary<string, object?>(),
    };
}
