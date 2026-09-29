using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Runs;

public class RunDataSanitizerTests
{
    private const string ActionAlias = "test.sensitiveSettings";

    private readonly RunDataSanitizer _sanitizer;

    public RunDataSanitizerTests()
    {
        var infrastructure = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());
        var actions = new ActionCollection(() => [new SensitiveSettingsAction(infrastructure)]);
        var controlFlows = new ControlFlowCollection(() => []);
        _sanitizer = new RunDataSanitizer(actions, controlFlows, NullLogger<RunDataSanitizer>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    public void Sanitize_NothingRecorded_ReturnsEmpty(string? json)
    {
        var result = _sanitizer.SanitizeStepOutput(ActionAlias, json);

        result.Value.ShouldBeNull();
        result.Truncated.ShouldBeFalse();
    }

    [Fact]
    public void SanitizeStepInput_MasksSchemaSensitiveFieldsAndKeyPatterns()
    {
        var result = _sanitizer.SanitizeStepInput(
            ActionAlias,
            """{"signingKey":"s3cr3t","channel":"#general","extra":{"password":"p"}}""");

        var node = JsonNode.Parse(result.Value!)!;
        node["signingKey"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        node["extra"]!["password"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        node["channel"]!.GetValue<string>().ShouldBe("#general");
        result.Value.ShouldNotContain("s3cr3t");
    }

    [Fact]
    public void SanitizeStepInput_UnknownAction_StillMasksKeyPatterns()
    {
        var result = _sanitizer.SanitizeStepInput("test.gone", """{"signingKey":"visible","token":"hidden"}""");

        var node = JsonNode.Parse(result.Value!)!;
        node["signingKey"]!.GetValue<string>().ShouldBe("visible");
        node["token"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
    }

    [Fact]
    public void SanitizeStepOutput_DoesNotApplyTheSettingsSchema()
    {
        // The settings schema describes the input; an output property that happens to share a
        // sensitive setting's name is only masked if it matches a key pattern.
        var result = _sanitizer.SanitizeStepOutput(ActionAlias, """{"signingKey":"output-value","token":"t"}""");

        var node = JsonNode.Parse(result.Value!)!;
        node["signingKey"]!.GetValue<string>().ShouldBe("output-value");
        node["token"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
    }

    [Fact]
    public void SanitizeTriggerData_MasksAndPrettyPrints()
    {
        var result = _sanitizer.SanitizeTriggerData("""{"headers":{"Cookie":"a=b"},"body":"<p>Hi & bye</p>"}""");

        result.Value.ShouldContain(Environment.NewLine);
        result.Value.ShouldContain("<p>Hi & bye</p>");
        result.Value.ShouldNotContain("a=b");
        result.Truncated.ShouldBeFalse();
    }

    [Fact]
    public void Sanitize_ValueOverTheCap_IsTruncatedAndFlagged()
    {
        var big = new string('x', IRunDataSanitizer.MaxValueLength * 2);

        var result = _sanitizer.SanitizeStepOutput(ActionAlias, $$"""{"payload":"{{big}}"}""");

        result.Truncated.ShouldBeTrue();
        result.Value!.Length.ShouldBe(IRunDataSanitizer.MaxValueLength);
    }

    [Fact]
    public void Sanitize_ValueAtTheCap_IsNotTruncated()
    {
        // {"payload":"…"} pretty-printed is `{` + newline + `  "payload": "` … `"` + newline + `}`.
        var overhead = $"{{{Environment.NewLine}  \"payload\": \"\"{Environment.NewLine}}}".Length;
        var exact = new string('x', IRunDataSanitizer.MaxValueLength - overhead);

        var result = _sanitizer.SanitizeStepOutput(ActionAlias, $$"""{"payload":"{{exact}}"}""");

        result.Truncated.ShouldBeFalse();
        result.Value!.Length.ShouldBe(IRunDataSanitizer.MaxValueLength);
    }

    [Fact]
    public void Sanitize_TruncationDoesNotSplitASurrogatePair()
    {
        // Pad so a surrogate pair straddles the cut point.
        var prefixLength = $"{{{Environment.NewLine}  \"payload\": \"".Length;
        var padding = new string('x', IRunDataSanitizer.MaxValueLength - prefixLength - 1);
        var json = $$"""{"payload":"{{padding}}😀😀😀"}""";

        var result = _sanitizer.SanitizeStepOutput(ActionAlias, json);

        result.Truncated.ShouldBeTrue();
        char.IsHighSurrogate(result.Value![^1]).ShouldBeFalse();
    }

    [Fact]
    public void Sanitize_MaskingHappensBeforeTruncation()
    {
        var big = new string('x', IRunDataSanitizer.MaxValueLength);

        var result = _sanitizer.SanitizeStepOutput(ActionAlias, $$"""{"token":"super-secret","payload":"{{big}}"}""");

        result.Truncated.ShouldBeTrue();
        result.Value.ShouldNotContain("super-secret");
        result.Value.ShouldContain(SensitiveDataMasker.MaskedValue);
    }

    [Fact]
    public void Sanitize_UnparseableJson_IsWithheld()
    {
        var result = _sanitizer.SanitizeStepOutput(ActionAlias, "{ token: not-json");

        result.Value.ShouldBeNull();
        result.Truncated.ShouldBeFalse();
    }

    [Action(ActionAlias, "Sensitive Settings")]
    private sealed class SensitiveSettingsAction(ActionInfrastructure infrastructure)
        : ActionBase<SensitiveSettings, object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private sealed class SensitiveSettings
    {
        [Field(Label = "Signing key", IsSensitive = true)]
        public string? SigningKey { get; set; }

        [Field(Label = "Channel")]
        public string? Channel { get; set; }
    }
}
