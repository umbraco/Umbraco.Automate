using Shouldly;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class TriggerEventTests
{
    [Fact]
    public void TriggerEvent_SetsRequiredProperties()
    {
        var evt = new TriggerEvent
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
            InitiatorId = "user-123",
        };

        evt.TriggerAlias.ShouldBe("contentPublished");
        evt.InitiatorType.ShouldBe("system");
        evt.InitiatorId.ShouldBe("user-123");
    }

    [Fact]
    public void TypedTriggerEvent_ContainsOutput()
    {
        var evt = new TriggerEvent<TestOutput>
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
            Output = new TestOutput { Name = "Test" },
        };

        evt.Output.Name.ShouldBe("Test");
    }

    [Fact]
    public void TypedTriggerEvent_IsPatternMatchableViaInterface()
    {
        TriggerEvent evt = new TriggerEvent<TestOutput>
        {
            TriggerAlias = "contentPublished",
            InitiatorType = "system",
            Output = new TestOutput { Name = "Test" },
        };

        evt.ShouldBeAssignableTo<ITriggerEventWithOutput>();

        var typed = (ITriggerEventWithOutput)evt;
        var output = typed.GetOutput();

        output.ShouldBeOfType<TestOutput>();
        ((TestOutput)output).Name.ShouldBe("Test");
    }

    [Fact]
    public void UntypedTriggerEvent_DoesNotImplementInterface()
    {
        var evt = new TriggerEvent
        {
            TriggerAlias = "manual",
            InitiatorType = "user",
        };

        evt.ShouldNotBeAssignableTo<ITriggerEventWithOutput>();
    }

    private class TestOutput
    {
        public string Name { get; set; } = string.Empty;
    }
}
