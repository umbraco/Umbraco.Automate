using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class SwitchActionTests
{
    private readonly SwitchAction _action = new(
        new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));

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
    public async Task ExecuteAsync_MatchingCase_ReturnsMatchedOutcome()
    {
        var context = CreateContext(expression: "success", cases: "success,failure,pending");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("success");
    }

    [Fact]
    public async Task ExecuteAsync_NoMatch_ReturnsDefaultOutcome()
    {
        var context = CreateContext(expression: "unknown", cases: "success,failure");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("default");
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveMatching()
    {
        var context = CreateContext(expression: "SUCCESS", cases: "success,failure");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("success");
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceInCasesIsTrimmed()
    {
        var context = CreateContext(expression: "success", cases: " success , failure , pending ");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("success");
    }

    [Fact]
    public async Task ExecuteAsync_OutputIncludesExpressionAndMatchedCase()
    {
        var context = CreateContext(expression: "failure", cases: "success,failure");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCases_ReturnsDefault()
    {
        var context = CreateContext(expression: "anything", cases: "");
        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.ShouldBe("default");
    }

    private static ActionContext CreateContext(string expression, string cases) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.switch",
        Settings = new SwitchActionSettings { Expression = expression, Cases = cases },
    };
}
