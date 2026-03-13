using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class SetVariableActionTests
{
    private readonly SetVariableAction _action = new(
        new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.setVariable");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Set Variable");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(SetVariableActionSettings));

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithNameAndValue()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.setVariable",
            Settings = new SetVariableActionSettings { Name = "myVar", Value = "hello" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_DefaultSettings_DoesNotThrow()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.setVariable",
            Settings = new SetVariableActionSettings(),
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);
        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyNameAndValue_StillSucceeds()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.setVariable",
            Settings = new SetVariableActionSettings { Name = "", Value = "" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);
        result.Status.ShouldBe(ActionResultStatus.Success);
    }
}
