using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public sealed class McpTriggerSettingsTests
{
    [Fact]
    public void DefaultTimeoutSeconds_Is30()
    {
        new McpTriggerSettings().TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void DefaultInputFields_IsEmpty()
    {
        new McpTriggerSettings().InputFields.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultSecret_IsNull()
    {
        new McpTriggerSettings().Secret.ShouldBeNull();
    }
}
