using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;
using static Umbraco.Automate.Tests.Unit.Automations.SensitiveSettingsTestHelper;

namespace Umbraco.Automate.Tests.Unit.Automations.Transfer;

public class SensitiveSettingsStripperTests
{
    private static SensitiveSettingsStripper BuildStripper(
        IEnumerable<IConnectionType>? connectionTypes = null,
        IEnumerable<INotificationChannel>? channels = null)
        => new(
            new ActionCollection(() => []),
            new TriggerCollection(() => []),
            new ControlFlowCollection(() => []),
            new ConnectionTypeCollection(() => connectionTypes ?? []),
            new WebhookAuthenticatorCollection(() => [new PlainSecretWebhookAuthenticator()]),
            new NotificationChannelCollection(() => channels ?? []));

    private static INotificationChannel BuildChannel(string alias, EditableModelSchema? schema)
    {
        var mock = new Mock<INotificationChannel>();
        mock.SetupGet(x => x.Alias).Returns(alias);
        mock.Setup(x => x.GetSettingsSchema()).Returns(schema);
        return mock.Object;
    }

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

    [Fact]
    public void StripTrigger_WithWebhookAuthenticatorBoundFromJson_RemovesStrategySecret()
    {
        var stripper = BuildStripper();

        var result = stripper.StripTrigger(WebhookTriggerFromJson("s3cret"));

        result.ShouldNotBeNull();
        var auth = SettingsDictionary.From(result.Settings["authenticator"]).ShouldNotBeNull();
        ReadString(auth["alias"]).ShouldBe(PlainSecretWebhookAuthenticator.WellKnownAlias);
        SettingsDictionary.From(auth["settings"]).ShouldNotBeNull().ShouldNotContainKey("secret");
    }

    [Fact]
    public void StripNotificationSettings_RemovesChannelSecret_KeepsTheRest()
    {
        var channel = BuildChannel(ChannelAlias, Schema(("url", false), (ChannelSecretKey, true)));
        var stripper = BuildStripper(channels: [channel]);

        var result = stripper.StripNotificationSettings(Channels("hmac-key"));

        result.ShouldNotBeNull();
        var stripped = result.Channels.ShouldHaveSingleItem();
        stripped.Settings.ShouldNotContainKey(ChannelSecretKey);
        stripped.Settings.ShouldContainKey("url");
        stripped.ChannelAlias.ShouldBe(ChannelAlias);
        stripped.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void StripNotificationSettings_WithUnknownChannel_ReturnsChannelUnchanged()
    {
        var stripper = BuildStripper();
        var settings = Channels("hmac-key");

        var result = stripper.StripNotificationSettings(settings);

        result.ShouldNotBeNull().Channels.ShouldHaveSingleItem().ShouldBeSameAs(settings.Channels[0]);
    }
}
