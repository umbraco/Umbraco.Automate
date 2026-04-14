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

    [Fact]
    public void Build_WithJsonArrayItem_ParsesToPrimitive()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        // A JSON array string should be unwrapped into a List<object?>.
        var jsonItem = """["alpha","beta","gamma"]""";
        var iteration = new ForEachIterationContext(jsonItem, 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        var item = loop["item"].ShouldBeOfType<List<object?>>();
        item.Count.ShouldBe(3);
        item[0].ShouldBe("alpha");
    }

    [Fact]
    public void Build_WithNestedJsonObjectItem_ParsesRecursively()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var jsonItem = """{"name":"Page","tags":["tag1","tag2"]}""";
        var iteration = new ForEachIterationContext(jsonItem, 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        var item = loop["item"].ShouldBeOfType<Dictionary<string, object?>>();
        item["name"].ShouldBe("Page");
        var tags = item["tags"].ShouldBeOfType<List<object?>>();
        tags.Count.ShouldBe(2);
        tags[0].ShouldBe("tag1");
    }

    [Fact]
    public void Build_WithAlreadyStructuredItem_ReturnsAsIs()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        // Items that are already structured (not strings) should pass through unchanged.
        var structuredItem = new Dictionary<string, object?> { ["name"] = "Already Parsed" };
        var iteration = new ForEachIterationContext(structuredItem, 0);
        var result = BindingDataBuilder.Build(data, iteration);

        var loop = result["loop"].ShouldBeOfType<Dictionary<string, object?>>();
        var item = loop["item"].ShouldBeOfType<Dictionary<string, object?>>();
        item["name"].ShouldBe("Already Parsed");
    }

    // --- Step alias tests ---

    [Fact]
    public void Build_WithStepAliases_RegistersBothGuidAndAliasKeys()
    {
        var stepId = Guid.NewGuid();
        var outputs = new Dictionary<string, object?> { ["statusCode"] = 200 };

        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [stepId] = outputs },
            StepAliases = new Dictionary<Guid, string> { [stepId] = "httpRequest" },
        };

        var result = BindingDataBuilder.Build(data);

        var steps = result["steps"].ShouldBeOfType<Dictionary<string, object?>>();
        steps[stepId.ToString()].ShouldBeSameAs(outputs);
        steps["httpRequest"].ShouldBeSameAs(outputs);
    }

    [Fact]
    public void Build_WithoutStepAliases_OnlyGuidKey()
    {
        var stepId = Guid.NewGuid();
        var outputs = new Dictionary<string, object?> { ["statusCode"] = 200 };

        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [stepId] = outputs },
        };

        var result = BindingDataBuilder.Build(data);

        var steps = result["steps"].ShouldBeOfType<Dictionary<string, object?>>();
        steps.ShouldContainKey(stepId.ToString());
        steps.Count.ShouldBe(1);
    }

    // --- Previous tests ---

    [Fact]
    public void Build_WithLastCompletedStepId_AddsPreviousKey()
    {
        var stepId = Guid.NewGuid();
        var outputs = new Dictionary<string, object?> { ["messageId"] = "msg-123" };

        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [stepId] = outputs },
            LastCompletedStepId = stepId,
        };

        var result = BindingDataBuilder.Build(data);

        result.ShouldContainKey("previous");
        result["previous"].ShouldBeSameAs(outputs);
    }

    [Fact]
    public void Build_WithoutLastCompletedStepId_NoPreviousKey()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
        };

        var result = BindingDataBuilder.Build(data);

        result.ShouldNotContainKey("previous");
    }

    [Fact]
    public void Build_WithLastCompletedStepId_NoMatchingOutputs_NoPreviousKey()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            LastCompletedStepId = Guid.NewGuid(), // No matching output
        };

        var result = BindingDataBuilder.Build(data);

        result.ShouldNotContainKey("previous");
    }
}
