using Shouldly;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class BindingDataBuilderTests
{
    [Fact]
    public void Build_WithoutIterationContext_HasNoLoopKey()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?> { ["name"] = "test" },
        };

        var result = BindingDataBuilder.Build(data);

        result.ShouldContainKey("trigger");
        result.ShouldContainKey("steps");
        result.ShouldNotContainKey("loop");
    }

    [Fact]
    public void Build_WithIterationContext_HasLoopKey()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var iteration = new ForEachIterationContext("hello", 2);
        var result = BindingDataBuilder.Build(data, iteration);

        result.ShouldContainKey("loop");
        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        loop["item"].ShouldBe("hello");
        loop["index"].ShouldBe(2);
    }

    [Fact]
    public void Build_WithJsonObjectItem_ParsesToDictionary()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var jsonItem = """{"contentKey":"abc-123","contentName":"Page One"}""";
        var iteration = new ForEachIterationContext(jsonItem, 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        var item = loop["item"].ShouldBeOfType<Dictionary<string, object?>>();
        item["contentKey"].ShouldBe("abc-123");
        item["contentName"].ShouldBe("Page One");
    }

    [Fact]
    public void Build_WithSimpleStringItem_ReturnsAsIs()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var iteration = new ForEachIterationContext("simple-value", 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        loop["item"].ShouldBe("simple-value");
    }

    [Fact]
    public void Build_WithNullIterationItem_ReturnsNull()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var iteration = new ForEachIterationContext(null, 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        loop["item"].ShouldBeNull();
        loop["index"].ShouldBe(0);
    }
}
