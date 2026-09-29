using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Automations;

/// <summary>
/// Builds an <see cref="AutomationSettingsProtector"/> wired to one action, one notification channel
/// and the real webhook trigger, each with a sensitive field, and a fake cipher that prefixes <c>ENC:</c>.
/// </summary>
internal static class SensitiveSettingsTestHelper
{
    public const string ActionAlias = "test.sendRequest";
    public const string ActionSecretKey = "headers";
    public const string ChannelAlias = "test.webhookChannel";
    public const string ChannelSecretKey = "secret";

    public static ISensitiveFieldProtector CreateFieldProtector()
    {
        var protector = new Mock<ISensitiveFieldProtector>();
        protector.Setup(p => p.IsProtected(It.IsAny<string>()))
            .Returns((string? v) => v?.StartsWith("ENC:", StringComparison.Ordinal) == true);
        protector.Setup(p => p.Protect(It.IsAny<string>()))
            .Returns((string? v) => string.IsNullOrEmpty(v) || v.StartsWith("ENC:", StringComparison.Ordinal) ? v : $"ENC:{v}");
        protector.Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns((string? v) => v is not null && v.StartsWith("ENC:", StringComparison.Ordinal) ? v[4..] : v);
        return protector.Object;
    }

    public static EditableModelSerializer CreateSerializer()
        => new(CreateFieldProtector(), CreateConfigurationReferenceResolver());

    public static AutomationSettingsProtector CreateSettingsProtector(IEditableModelSerializer? serializer = null)
    {
        serializer ??= CreateSerializer();

        var action = new Mock<IAction>();
        action.SetupGet(a => a.Alias).Returns(ActionAlias);
        action.Setup(a => a.GetSettingsSchema()).Returns(Schema((ActionSecretKey, true), ("url", false)));

        var channel = new Mock<INotificationChannel>();
        channel.SetupGet(c => c.Alias).Returns(ChannelAlias);
        channel.Setup(c => c.GetSettingsSchema()).Returns(Schema(("url", false), (ChannelSecretKey, true)));

        var modelResolver = new EditableModelResolver(CreateConfigurationReferenceResolver());

        return new AutomationSettingsProtector(
            serializer,
            new ActionCollection(() => [action.Object]),
            new TriggerCollection(() => [new WebhookTrigger(new TriggerInfrastructure(modelResolver))]),
            new WebhookAuthenticatorCollection(() => [new PlainSecretWebhookAuthenticator(), new HmacSha256WebhookAuthenticator()]),
            new NotificationChannelCollection(() => [channel.Object]));
    }

    public static StepConfiguration Step(string secret, string url = "https://example.com", string actionAlias = ActionAlias)
        => new()
        {
            Id = Guid.NewGuid(),
            ActionAlias = actionAlias,
            Name = "Send request",
            Settings = new Dictionary<string, object?> { [ActionSecretKey] = secret, ["url"] = url },
        };

    public static AutomationNotificationSettings Channels(string secret, string url = "https://example.com/hook")
        => new()
        {
            Channels =
            [
                new ChannelConfiguration
                {
                    ChannelAlias = ChannelAlias,
                    Settings = new Dictionary<string, object?> { ["url"] = url, [ChannelSecretKey] = secret },
                },
            ],
        };

    /// <summary>
    /// A webhook trigger whose settings are shaped the way the Management API binds them:
    /// nested values arrive as <see cref="JsonElement"/>, not as dictionaries of strings.
    /// </summary>
    public static TriggerConfiguration WebhookTriggerFromJson(string secret)
        => new()
        {
            TriggerAlias = WebhookTrigger.WellKnownAlias,
            Settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                JsonSerializer.Serialize(new
                {
                    authenticator = new
                    {
                        alias = PlainSecretWebhookAuthenticator.WellKnownAlias,
                        settings = new { secret },
                    },
                }))!,
        };

    public static string? ReadString(object? value) => value switch
    {
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
        _ => null,
    };

    public static string? ReadWebhookSecret(TriggerConfiguration trigger)
    {
        var auth = SettingsDictionary.From(trigger.Settings["authenticator"])!;
        var settings = SettingsDictionary.From(auth["settings"])!;
        return ReadString(settings["secret"]);
    }

    private static IConfigurationReferenceResolver CreateConfigurationReferenceResolver()
        => new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

    private static EditableModelSchema Schema(params (string key, bool isSensitive)[] fields) => new()
    {
        Fields = fields
            .Select(f => new EditableModelFieldDescriptor
            {
                Key = f.key,
                Label = f.key,
                PropertyName = f.key,
                PropertyType = typeof(string),
                IsSensitive = f.isSensitive,
            })
            .ToList(),
    };
}
