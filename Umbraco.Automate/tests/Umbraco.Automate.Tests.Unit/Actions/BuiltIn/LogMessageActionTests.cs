using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class LogMessageActionTests
{
    private readonly LogMessageAction _action = new(
        new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
        Mock.Of<ILogger<LogMessageAction>>());

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.logMessage");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Log Message");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(LogMessageSettings));

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessWithMessage()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Settings = new LogMessageSettings { Message = "Hello World", LogLevel = "Information" },
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
            ActionAlias = "umbracoAutomate.logMessage",
            Settings = new LogMessageSettings(),
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);
        result.Status.ShouldBe(ActionResultStatus.Success);
    }
}
