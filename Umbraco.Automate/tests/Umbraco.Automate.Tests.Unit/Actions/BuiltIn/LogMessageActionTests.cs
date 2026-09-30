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

    [Fact]
    public void DefaultLogLevel_IsInformation()
        => new LogMessageSettings().LogLevel.ShouldBe("Information");

    // The Log Level setting is rendered as a dropdown (see LogMessageSettings).
    // Each offered option must execute successfully so the picker never lets the
    // user select a value the action cannot handle.
    [Theory]
    [InlineData("Debug")]
    [InlineData("Information")]
    [InlineData("Warning")]
    [InlineData("Error")]
    public async Task ExecuteAsync_WithDropdownLogLevel_Succeeds(string logLevel)
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Settings = new LogMessageSettings { Message = "Hello", LogLevel = logLevel },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Theory]
    [InlineData("Debug", ActionLogLevel.Debug)]
    [InlineData("Information", ActionLogLevel.Info)]
    [InlineData("Warning", ActionLogLevel.Warning)]
    [InlineData("Error", ActionLogLevel.Error)]
    public async Task ExecuteAsync_RecordsLogEntry_AtExpectedLevel(string logLevel, ActionLogLevel expected)
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.logMessage",
            Settings = new LogMessageSettings { Message = "Hello", LogLevel = logLevel },
            MinimumLogLevel = ActionLogLevel.Debug,
        };

        await _action.ExecuteAsync(context, CancellationToken.None);

        var entry = context.LogEntries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(expected);
        entry.Message.ShouldBe("Hello");
    }
}
