using Microsoft.Extensions.Configuration;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
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

    // These two go through the real resolution/validation pipeline (EditableModelResolver +
    // the schema McpTrigger builds for McpTriggerSettings) rather than asserting the
    // [RegularExpression] attribute is merely present, so a regression in how
    // EditableModelSchemaBuilder wires ValidationAttributes into the schema would also be
    // caught here, matching the pattern McpAuthenticationMiddlewareTests already uses for the
    // same reason (a bare Mock.Of<IEditableModelResolver>() would skip validation entirely).
    private static McpTrigger CreateTrigger()
    {
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        return new McpTrigger(new TriggerInfrastructure(modelResolver));
    }

    [Fact]
    public void ResolveSettings_ToolNameWithSpace_ThrowsValidationException()
    {
        var trigger = CreateTrigger();
        var settings = new Dictionary<string, object?>
        {
            ["toolName"] = "Send welcome email",
            ["toolDescription"] = "Sends the welcome email.",
        };

        Should.Throw<InvalidOperationException>(() => trigger.ResolveSettings(settings));
    }

    [Fact]
    public void ResolveSettings_ValidToolName_ResolvesSuccessfully()
    {
        var trigger = CreateTrigger();
        var settings = new Dictionary<string, object?>
        {
            ["toolName"] = "send_welcome-email1",
            ["toolDescription"] = "Sends the welcome email.",
        };

        var resolved = trigger.ResolveSettings(settings);

        resolved!.ToolName.ShouldBe("send_welcome-email1");
    }
}
