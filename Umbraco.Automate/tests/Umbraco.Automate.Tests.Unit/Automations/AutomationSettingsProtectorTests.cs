using Umbraco.Automate.Core.Automations;
using static Umbraco.Automate.Tests.Unit.Automations.SensitiveSettingsTestHelper;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class AutomationSettingsProtectorTests
{
    private readonly AutomationSettingsProtector _protector = CreateSettingsProtector();

    [Fact]
    public void ProtectStep_EncryptsSensitiveSetting_AndLeavesOthers()
    {
        var result = _protector.ProtectStep(Step("X-Api-Key: 123"));

        ReadString(result.Settings[ActionSecretKey]).ShouldBe("ENC:X-Api-Key: 123");
        ReadString(result.Settings["url"]).ShouldBe("https://example.com");
    }

    [Fact]
    public void ProtectStep_WhenAlreadyEncrypted_DoesNotEncryptAgain()
    {
        var result = _protector.ProtectStep(Step("ENC:already"));

        ReadString(result.Settings[ActionSecretKey]).ShouldBe("ENC:already");
    }

    [Fact]
    public void ProtectStep_WithUnregisteredAction_ReturnsSameInstance()
    {
        var step = Step("plain", actionAlias: "not.registered");

        _protector.ProtectStep(step).ShouldBeSameAs(step);
    }

    [Fact]
    public void ProtectTrigger_WithWebhookAuthenticatorBoundFromJson_EncryptsSecret()
    {
        // The Management API binds nested trigger settings as JsonElement. The authenticator alias
        // must still be read, or the strategy's secret is stored unencrypted.
        var result = _protector.ProtectTrigger(WebhookTriggerFromJson("s3cret"));

        result.ShouldNotBeNull();
        ReadWebhookSecret(result).ShouldBe("ENC:s3cret");
    }

    [Fact]
    public void ProtectNotificationSettings_EncryptsChannelSecret_AndLeavesOthers()
    {
        var result = _protector.ProtectNotificationSettings(Channels("hmac-key"));

        result.ShouldNotBeNull();
        var channel = result.Channels.ShouldHaveSingleItem();
        ReadString(channel.Settings[ChannelSecretKey]).ShouldBe("ENC:hmac-key");
        ReadString(channel.Settings["url"]).ShouldBe("https://example.com/hook");
        channel.ChannelAlias.ShouldBe(ChannelAlias);
        channel.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void UnprotectAutomationSettings_DecryptsTriggerStepsAndChannels()
    {
        var automation = new Automation
        {
            Alias = "a",
            Name = "A",
            Trigger = WebhookTriggerFromJson("ENC:s3cret"),
            Steps = [Step("ENC:header-value"), Step("ENC:second")],
            NotificationSettings = Channels("ENC:hmac-key"),
        };

        _protector.UnprotectAutomationSettings(automation);

        ReadWebhookSecret(automation.Trigger!).ShouldBe("s3cret");
        ReadString(automation.Steps[0].Settings[ActionSecretKey]).ShouldBe("header-value");
        ReadString(automation.Steps[1].Settings[ActionSecretKey]).ShouldBe("second");
        ReadString(automation.NotificationSettings!.Channels[0].Settings[ChannelSecretKey]).ShouldBe("hmac-key");
    }

    [Fact]
    public void ProtectAutomationSettings_ThenUnprotect_RoundTrips()
    {
        var automation = new Automation
        {
            Alias = "a",
            Name = "A",
            Trigger = WebhookTriggerFromJson("s3cret"),
            Steps = [Step("header-value")],
            NotificationSettings = Channels("hmac-key"),
        };

        _protector.ProtectAutomationSettings(automation);
        ReadString(automation.Steps[0].Settings[ActionSecretKey]).ShouldBe("ENC:header-value");

        _protector.UnprotectAutomationSettings(automation);
        ReadWebhookSecret(automation.Trigger!).ShouldBe("s3cret");
        ReadString(automation.Steps[0].Settings[ActionSecretKey]).ShouldBe("header-value");
        ReadString(automation.NotificationSettings!.Channels[0].Settings[ChannelSecretKey]).ShouldBe("hmac-key");
    }

    [Fact]
    public void GetSensitiveKeys_ReturnsDeclaredKeys_CaseInsensitively()
    {
        var keys = _protector.GetSensitiveKeys(SensitiveSettingsOwner.Action, ActionAlias);

        keys.ShouldContain("HEADERS");
        keys.ShouldNotContain("url");
    }

    [Fact]
    public void GetSensitiveKeys_WithUnregisteredOwner_ReturnsEmpty()
        => _protector.GetSensitiveKeys(SensitiveSettingsOwner.NotificationChannel, "missing").ShouldBeEmpty();
}
