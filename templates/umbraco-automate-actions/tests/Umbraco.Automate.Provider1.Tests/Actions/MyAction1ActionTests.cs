using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Provider1.Actions;
using Umbraco.Automate.Testing;
using Xunit;

namespace Umbraco.Automate.Provider1.Tests.Actions;

public class MyAction1ActionTests
{
    [Fact]
    public async Task Execute_WithMessage_ReturnsProcessedOutput()
    {
        // Arrange
        var result = await ActionTestHarness
            .For<MyAction1Action>()
            .WithSettings(new MyAction1Settings { Message = "Hello" })
            .ExecuteAsync();

        // Assert
        Assert.Equal(ActionStatus.Succeeded, result.Status);
        Assert.NotNull(result.OutputData);
        Assert.Equal("Processed: Hello", result.OutputData["processedMessage"]);
    }

    [Fact]
    public async Task Execute_WithEmptyMessage_ReturnsFailed()
    {
        // Arrange
        var result = await ActionTestHarness
            .For<MyAction1Action>()
            .WithSettings(new MyAction1Settings { Message = "" })
            .ExecuteAsync();

        // Assert
        Assert.Equal(ActionStatus.Failed, result.Status);
    }
}
