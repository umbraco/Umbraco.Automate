using Shouldly;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Tests.Unit.Actions;

public class ActionContextTests
{
    private static ActionContext CreateContext(ActionLogLevel minimumLogLevel = ActionLogLevel.Debug, int maxLogEntries = 200)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "test.action",
            MinimumLogLevel = minimumLogLevel,
            MaxLogEntries = maxLogEntries,
        };

    [Fact]
    public void Log_RecordsEntry_WhenAtOrAboveMinimumLevel()
    {
        var context = CreateContext(ActionLogLevel.Info);

        context.LogInfo("hello");
        context.LogWarning("careful");
        context.LogError("boom");

        context.LogEntries.Count.ShouldBe(3);
        context.LogEntries[0].Level.ShouldBe(ActionLogLevel.Info);
        context.LogEntries[0].Message.ShouldBe("hello");
        context.LogEntries[1].Level.ShouldBe(ActionLogLevel.Warning);
        context.LogEntries[2].Level.ShouldBe(ActionLogLevel.Error);
    }

    [Fact]
    public void Log_DropsEntry_BelowMinimumLevel()
    {
        var context = CreateContext(ActionLogLevel.Warning);

        context.LogDebug("debug detail");
        context.LogInfo("progress");
        context.LogWarning("careful");

        context.LogEntries.ShouldHaveSingleItem();
        context.LogEntries[0].Level.ShouldBe(ActionLogLevel.Warning);
    }

    [Fact]
    public void Log_StopsRecording_PastMaxLogEntries()
    {
        var context = CreateContext(ActionLogLevel.Debug, maxLogEntries: 2);

        context.LogInfo("one");
        context.LogInfo("two");
        context.LogInfo("three");

        context.LogEntries.Count.ShouldBe(2);
    }
}
