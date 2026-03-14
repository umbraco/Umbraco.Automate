using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class SwitchActionTests
{
    private readonly SwitchAction _action;

    public SwitchActionTests()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);
        _action = new SwitchAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            conditionEvaluator);
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.switch");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Switch");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(SwitchActionSettings));

    [Fact]
    public async Task ExecuteAsync_FirstMatchingCase_ReturnsItsOutcome()
    {
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "high",
                    Conditions = BuildEquals("100", "100"),
                },
                new SwitchCase
                {
                    Name = "low",
                    Conditions = BuildEquals("100", "50"),
                },
            ],
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("high");
    }

    [Fact]
    public async Task ExecuteAsync_SecondCaseMatches_ReturnsSecondOutcome()
    {
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "high",
                    Conditions = BuildEquals("50", "100"),
                },
                new SwitchCase
                {
                    Name = "low",
                    Conditions = BuildEquals("50", "50"),
                },
            ],
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("low");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_ReturnsDefault()
    {
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "high",
                    Conditions = BuildEquals("a", "b"),
                },
            ],
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("default");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCases_ReturnsDefault()
    {
        var settings = new SwitchActionSettings { Cases = [] };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Outcome.ShouldBe("default");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyConditionSet_AlwaysMatches()
    {
        // An empty ConditionSet is vacuously true — useful as a catch-all fallback case.
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "fallback",
                    Conditions = new ConditionSet(),
                },
            ],
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.Outcome.ShouldBe("fallback");
    }

    [Fact]
    public async Task ExecuteAsync_WithBindings_ResolvesAgainstData()
    {
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase
                {
                    Name = "published",
                    Conditions = BuildEquals("${ trigger.status }", "published"),
                },
            ],
        };

        var bindingData = new Dictionary<string, object?>
        {
            ["trigger"] = new Dictionary<string, object?> { ["status"] = "published" },
        };

        var context = CreateContext(settings, bindingData);
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.ShouldBe("published");
    }

    [Fact]
    public async Task ExecuteAsync_OutputIncludesMatchedCase()
    {
        var settings = new SwitchActionSettings
        {
            Cases =
            [
                new SwitchCase { Name = "match", Conditions = BuildEquals("a", "a") },
            ],
        };

        var result = await _action.ExecuteAsync(CreateContext(settings), CancellationToken.None);

        result.OutputData.ShouldNotBeNull();
    }

    private static ConditionSet BuildEquals(string left, string right) => new()
    {
        Groups =
        [
            new ConditionGroup
            {
                Conditions = [new Condition { LeftOperand = left, Operator = ConditionOperator.Equals, RightOperand = right }],
            },
        ],
    };

    private static ActionContext CreateContext(SwitchActionSettings settings, Dictionary<string, object?>? bindingData = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.switch",
        Settings = settings,
        BindingData = bindingData ?? new Dictionary<string, object?>(),
    };
}
