using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class WebhookTriggerTests
{
    private readonly WebhookTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.webhook");

    [Fact]
    public void SupportsManualRun()
        => _trigger.ShouldBeAssignableTo<ISupportsManualRun>();

    [Fact]
    public void CreateManualRunOutput_WithNoSettings_UsesPostAndTheDefaultContentType()
    {
        var output = _trigger.CreateManualRunOutput(null);

        output.Success.ShouldBeTrue();
        var data = output.Data.ShouldNotBeNull();
        data["method"].ShouldBe("POST");
        data["body"].ShouldBeNull();
        Headers(data)["Content-Type"].ShouldBe("application/json");
    }

    [Fact]
    public void CreateManualRunOutput_UsesTheAllowedMethod()
    {
        var output = _trigger.CreateManualRunOutput(new WebhookTriggerSettings { AllowedMethod = "GET" });

        output.Data.ShouldNotBeNull()["method"].ShouldBe("GET");
    }

    [Fact]
    public void CreateManualRunOutput_WithBlankAllowedMethod_FallsBackToPost()
    {
        var output = _trigger.CreateManualRunOutput(new WebhookTriggerSettings { AllowedMethod = "  " });

        output.Data.ShouldNotBeNull()["method"].ShouldBe("POST");
    }

    [Fact]
    public void CreateManualRunOutput_PassesTheBodyThroughVerbatim()
    {
        var settings = new WebhookTriggerSettings { TestRequestBody = """{"title":"Hello"}""" };

        var output = _trigger.CreateManualRunOutput(settings);

        output.Data.ShouldNotBeNull()["body"].ShouldBe("""{"title":"Hello"}""");
    }

    [Fact]
    public void CreateManualRunOutput_PassesAMalformedBodyThroughUntouched()
    {
        // The live endpoint hands the body over as a string without parsing it, so a step being
        // tested against malformed input must see exactly that input.
        var settings = new WebhookTriggerSettings { TestRequestBody = "{not json" };

        var output = _trigger.CreateManualRunOutput(settings);

        output.Success.ShouldBeTrue();
        output.Data.ShouldNotBeNull()["body"].ShouldBe("{not json");
    }

    [Fact]
    public void CreateManualRunOutput_LayersSavedHeadersOverTheDefaults()
    {
        var settings = new WebhookTriggerSettings
        {
            TestRequestHeaders = """{ "X-Signature": "abc123" }""",
        };

        var output = _trigger.CreateManualRunOutput(settings);

        var headers = Headers(output.Data.ShouldNotBeNull());
        headers["X-Signature"].ShouldBe("abc123");
        headers["Content-Type"].ShouldBe("application/json");
    }

    [Fact]
    public void CreateManualRunOutput_LetsASavedHeaderOverrideTheDefault()
    {
        var settings = new WebhookTriggerSettings
        {
            TestRequestHeaders = """{ "Content-Type": "text/plain" }""",
        };

        var output = _trigger.CreateManualRunOutput(settings);

        Headers(output.Data.ShouldNotBeNull())["Content-Type"].ShouldBe("text/plain");
    }

    [Theory]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    [InlineData("not json at all")]
    [InlineData("null")]
    [InlineData("""{ "X-Count": 5 }""")]
    public void CreateManualRunOutput_WithHeadersThatAreNotAJsonObjectOfStrings_Fails(string headers)
    {
        var settings = new WebhookTriggerSettings { TestRequestHeaders = headers };

        var output = _trigger.CreateManualRunOutput(settings);

        output.Success.ShouldBeFalse();
        output.Data.ShouldBeNull();
        output.Error.ShouldNotBeNull().ShouldContain("test request headers");
    }

    [Fact]
    public void CreateManualRunOutput_WithBlankHeaders_KeepsJustTheDefault()
    {
        var settings = new WebhookTriggerSettings { TestRequestHeaders = "   " };

        var output = _trigger.CreateManualRunOutput(settings);

        output.Success.ShouldBeTrue();
        Headers(output.Data.ShouldNotBeNull()).Count.ShouldBe(1);
    }

    /// <summary>
    /// Nested values must arrive as plain dictionaries rather than <c>JsonElement</c>, so
    /// bindings can traverse <c>trigger.headers.Content-Type</c> and the values survive the
    /// Newtonsoft round-trip in the WorkflowCore persistence layer — the same guarantee the
    /// real webhook dispatch path gives.
    /// </summary>
    private static Dictionary<string, object?> Headers(Dictionary<string, object?> data)
        => data["headers"].ShouldBeAssignableTo<Dictionary<string, object?>>()!;
}
