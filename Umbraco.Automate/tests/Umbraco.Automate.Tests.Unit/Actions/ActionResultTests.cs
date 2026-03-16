using Shouldly;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Tests.Unit.Actions;

public class ActionResultTests
{
    [Fact]
    public void SuccessWithOutcome_SetsOutcomeProperty()
    {
        var result = ActionResult.SuccessWithOutcome("true", new { Value = 42 });

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("true");
        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public void SuccessWithOutcome_WithoutOutputData()
    {
        var result = ActionResult.SuccessWithOutcome("false");

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe("false");
        result.OutputData.ShouldBeNull();
    }

    [Fact]
    public void Success_LeavesOutcomeNull()
    {
        var result = ActionResult.Success(new { Value = 1 });

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public void Failed_LeavesOutcomeNull()
    {
        var result = ActionResult.Failed(new InvalidOperationException("test"));

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public void Skipped_LeavesOutcomeNull()
    {
        var result = ActionResult.Skipped("reason");

        result.Status.ShouldBe(ActionResultStatus.Skipped);
        result.Outcome.ShouldBeNull();
    }
}
