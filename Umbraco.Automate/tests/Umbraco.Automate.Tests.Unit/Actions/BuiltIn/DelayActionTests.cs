using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class DelayActionTests
{
    private readonly DelayAction _action;

    public DelayActionTests()
    {
        var infrastructure = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());
        _action = new DelayAction(infrastructure, Mock.Of<ILogger<DelayAction>>());
    }

    [Fact]
    public void Alias_Is_Correct()
    {
        _action.Alias.ShouldBe("umbracoAutomate.delay");
    }

    [Fact]
    public async Task ExecuteAsync_ValidDuration_Returns_Sleeping()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = _action.Alias,
            Settings = new DelaySettings { Duration = "00:05:00" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Sleeping);
        var suspension = result.Suspension.ShouldBeOfType<ActionSuspension.Sleep>();
        suspension.Duration.ShouldBe(TimeSpan.FromMinutes(5));
        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ZeroDuration_Returns_Success()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = _action.Alias,
            Settings = new DelaySettings { Duration = "00:00:00" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidDuration_Returns_Failed()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = _action.Alias,
            Settings = new DelaySettings { Duration = "not-a-timespan" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NegativeDuration_Returns_Failed()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = _action.Alias,
            Settings = new DelaySettings { Duration = "-00:05:00" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }
}
