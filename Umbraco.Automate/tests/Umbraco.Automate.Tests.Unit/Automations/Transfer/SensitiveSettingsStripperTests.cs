using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Automations.Transfer;

public class SensitiveSettingsStripperTests
{
    private static SensitiveSettingsStripper BuildStripper(IEnumerable<IConnectionType>? connectionTypes = null)
        => new(
            new ActionCollection(() => []),
            new TriggerCollection(() => []),
            new ControlFlowCollection(() => []),
            new ConnectionTypeCollection(() => connectionTypes ?? []));

    private static IConnectionType BuildConnectionType(string alias, EditableModelSchema? schema)
    {
        var mock = new Mock<IConnectionType>();
        mock.SetupGet(x => x.Alias).Returns(alias);
        mock.Setup(x => x.GetSettingsSchema()).Returns(schema);
        return mock.Object;
    }

    private static EditableModelSchema Schema(params (string property, bool isSensitive)[] fields) => new()
    {
        Fields = fields
            .Select(f => new EditableModelFieldDescriptor
            {
                Key = f.property,
                Label = f.property,
                PropertyName = f.property,
                PropertyType = typeof(string),
                IsSensitive = f.isSensitive,
            })
            .ToList(),
    };

    [Fact]
    public void StripConnectionSettings_WithUnknownAlias_ReturnsOriginal()
    {
        var stripper = BuildStripper();
        var settings = new Dictionary<string, object?> { ["ApiKey"] = "secret" };

        var result = stripper.StripConnectionSettings("missing", settings);

        result.ShouldBeSameAs(settings);
    }

    [Fact]
    public void StripConnectionSettings_WithNoSensitiveFields_ReturnsOriginal()
    {
        var connectionType = BuildConnectionType("httpBasic", Schema(("Endpoint", false)));
        var stripper = BuildStripper([connectionType]);
        var settings = new Dictionary<string, object?> { ["Endpoint"] = "https://example.com" };

        var result = stripper.StripConnectionSettings("httpBasic", settings);

        result.ShouldBeSameAs(settings);
    }

    [Fact]
    public void StripConnectionSettings_RemovesSensitiveFields_KeepsTheRest()
    {
        var connectionType = BuildConnectionType("httpBasic", Schema(("ApiKey", true), ("Endpoint", false)));
        var stripper = BuildStripper([connectionType]);
        var settings = new Dictionary<string, object?>
        {
            ["ApiKey"] = "secret",
            ["Endpoint"] = "https://example.com",
        };

        var result = stripper.StripConnectionSettings("httpBasic", settings);

        result.ShouldNotContainKey("ApiKey");
        result.ShouldContainKey("Endpoint");
    }

    [Fact]
    public void StripConnectionSettings_RemovesSensitiveField_RegardlessOfValueFormat()
    {
        // A field is sensitive by schema, not by value. $-config references and plaintext
        // should be stripped just like ENC: values when IgnoreSensitive is active.
        var connectionType = BuildConnectionType("httpBasic", Schema(("ApiKey", true)));
        var stripper = BuildStripper([connectionType]);
        var settings = new Dictionary<string, object?> { ["ApiKey"] = "$MyService:ApiKey" };

        var result = stripper.StripConnectionSettings("httpBasic", settings);

        result.ShouldNotContainKey("ApiKey");
    }

    [Fact]
    public void StripConnectionSettings_KeyMatching_IsCaseInsensitive()
    {
        // The schema's PropertyName is the canonical PascalCase form, but incoming
        // dictionaries may carry camelCase keys from JSON deserialization.
        var connectionType = BuildConnectionType("httpBasic", Schema(("ApiKey", true)));
        var stripper = BuildStripper([connectionType]);
        var settings = new Dictionary<string, object?> { ["apiKey"] = "secret" };

        var result = stripper.StripConnectionSettings("httpBasic", settings);

        result.ShouldNotContainKey("apiKey");
    }
}
