using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class ActionLogSanitizerTests
{
    private const string Mask = SensitiveDataMasker.MaskedValue;

    [Fact]
    public void CollectSensitiveValues_KeyPattern_CollectsTheValue()
    {
        var settings = JsonNode.Parse("""{ "url": "https://example.com", "apiKey": "abc123secret" }""");

        var values = ActionLogSanitizer.CollectSensitiveValues(settings, null);

        values.ShouldBe(["abc123secret"]);
    }

    [Fact]
    public void CollectSensitiveValues_SensitiveSchemaField_CollectsTheValue()
    {
        var settings = JsonNode.Parse("""{ "url": "https://example.com", "credential": "hunter2hunter2" }""");

        var values = ActionLogSanitizer.CollectSensitiveValues(settings, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "credential" });

        values.ShouldBe(["hunter2hunter2"]);
    }

    [Fact]
    public void CollectSensitiveValues_KeyValueRow_CollectsTheRowValue()
    {
        var settings = JsonNode.Parse("""{ "headers": [ { "key": "Authorization", "value": "Bearer xyz789" }, { "key": "Accept", "value": "application/json" } ] }""");

        var values = ActionLogSanitizer.CollectSensitiveValues(settings, null);

        values.ShouldBe(["Bearer xyz789"]);
    }

    [Fact]
    public void CollectSensitiveValues_EmbeddedJson_CollectsTheNestedValue()
    {
        var settings = JsonNode.Parse("""{ "body": "{\"password\":\"s3cretpass\",\"user\":\"bob\"}" }""");

        var values = ActionLogSanitizer.CollectSensitiveValues(settings, null);

        values.ShouldBe(["s3cretpass"]);
    }

    [Fact]
    public void CollectSensitiveValues_LeavesTheSettingsUnchanged()
    {
        var settings = JsonNode.Parse("""{ "token": "abc123secret" }""");

        ActionLogSanitizer.CollectSensitiveValues(settings, null);

        settings!["token"]!.GetValue<string>().ShouldBe("abc123secret");
    }

    [Fact]
    public void Sanitize_RedactsSensitiveValuesInMessages()
    {
        var entries = new[] { Entry("Calling API with key abc123secret, twice: abc123secret") };

        var result = ActionLogSanitizer.Sanitize(entries, ["abc123secret"]);

        result.ShouldHaveSingleItem().Message.ShouldBe($"Calling API with key {Mask}, twice: {Mask}");
    }

    [Fact]
    public void Sanitize_RedactsTheLongestValueFirst()
    {
        var entries = new[] { Entry("token=abcd-efgh") };

        var result = ActionLogSanitizer.Sanitize(entries, ["abcd", "abcd-efgh"]);

        result.ShouldHaveSingleItem().Message.ShouldBe($"token={Mask}");
    }

    [Fact]
    public void Sanitize_IgnoresVeryShortValues()
    {
        var entries = new[] { Entry("Created 12 items") };

        var result = ActionLogSanitizer.Sanitize(entries, ["12"]);

        result.ShouldHaveSingleItem().Message.ShouldBe("Created 12 items");
    }

    [Fact]
    public void Sanitize_TruncatesLongMessages()
    {
        var entries = new[] { Entry(new string('a', ActionLogSanitizer.MaxMessageLength + 500)) };

        var message = ActionLogSanitizer.Sanitize(entries, []).ShouldHaveSingleItem().Message;

        message.Length.ShouldBe(ActionLogSanitizer.MaxMessageLength);
        message.ShouldEndWith(ActionLogSanitizer.TruncatedSuffix);
    }

    [Fact]
    public void Sanitize_KeepsTimestampAndLevel()
    {
        var entry = new ActionLogEntry(new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc), ActionLogLevel.Warning, "key abc123secret");

        var result = ActionLogSanitizer.Sanitize([entry], ["abc123secret"]).ShouldHaveSingleItem();

        result.TimestampUtc.ShouldBe(entry.TimestampUtc);
        result.Level.ShouldBe(ActionLogLevel.Warning);
    }

    private static ActionLogEntry Entry(string message) => new(DateTime.UtcNow, ActionLogLevel.Info, message);
}
