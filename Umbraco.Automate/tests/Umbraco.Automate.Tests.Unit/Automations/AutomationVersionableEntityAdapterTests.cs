using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Versioning;
using static Umbraco.Automate.Tests.Unit.Automations.SensitiveSettingsTestHelper;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class AutomationVersionableEntityAdapterTests
{
    private readonly IVersionableEntityAdapter _adapter = new AutomationVersionableEntityAdapter(
        Mock.Of<IAutomationService>(),
        CreateSettingsProtector(),
        NullLogger<AutomationVersionableEntityAdapter>.Instance);

    private static Automation BuildAutomation(string stepSecret, string webhookSecret, string channelSecret, Guid? stepId = null)
    {
        var step = Step(stepSecret);
        step.Id = stepId ?? step.Id;

        return new Automation
        {
            Alias = "withSecrets",
            Name = "With secrets",
            Trigger = WebhookTriggerFromJson(webhookSecret),
            Steps = [step],
            NotificationSettings = Channels(channelSecret),
        };
    }

    [Fact]
    public void CreateSnapshot_EncryptsEverySensitiveSetting()
    {
        var snapshot = _adapter.CreateSnapshot(BuildAutomation("step-plain", "webhook-plain", "channel-plain"));

        snapshot.ShouldContain("ENC:step-plain");
        snapshot.ShouldContain("ENC:webhook-plain");
        snapshot.ShouldContain("ENC:channel-plain");
        snapshot.ShouldNotContain("\"step-plain\"");
        snapshot.ShouldNotContain("\"webhook-plain\"");
        snapshot.ShouldNotContain("\"channel-plain\"");
    }

    [Fact]
    public void CreateSnapshot_DoesNotModifyTheEntity()
    {
        var automation = BuildAutomation("step-plain", "webhook-plain", "channel-plain");

        _adapter.CreateSnapshot(automation);

        ReadString(automation.Steps[0].Settings[ActionSecretKey]).ShouldBe("step-plain");
        ReadString(automation.NotificationSettings!.Channels[0].Settings[ChannelSecretKey]).ShouldBe("channel-plain");
    }

    [Fact]
    public void RestoreFromSnapshot_DecryptsEverySensitiveSetting()
    {
        var snapshot = _adapter.CreateSnapshot(BuildAutomation("step-plain", "webhook-plain", "channel-plain"));

        var restored = _adapter.RestoreFromSnapshot(snapshot).ShouldBeOfType<Automation>();

        ReadString(restored.Steps[0].Settings[ActionSecretKey]).ShouldBe("step-plain");
        ReadWebhookSecret(restored.Trigger!).ShouldBe("webhook-plain");
        ReadString(restored.NotificationSettings!.Channels[0].Settings[ChannelSecretKey]).ShouldBe("channel-plain");
    }

    [Fact]
    public void CompareVersions_WhenSecretsChange_ReportsThemMasked()
    {
        var stepId = Guid.NewGuid();
        var from = BuildAutomation("old-step", "old-webhook", "old-channel", stepId);
        var to = BuildAutomation("new-step", "new-webhook", "new-channel", stepId);

        var changes = _adapter.CompareVersions(from, to);

        changes.ShouldContain(c => c.Path == $"Steps[{stepId}].Settings.{ActionSecretKey}");
        changes.ShouldContain(c => c.Path == "Trigger.Settings.authenticator.settings.secret");
        changes.ShouldContain(c => c.Path == $"NotificationSettings.Channels[0].Settings.{ChannelSecretKey}");

        foreach (var change in changes)
        {
            change.OldValue.ShouldBe(VersionComparer.MaskedValue);
            change.NewValue.ShouldBe(VersionComparer.MaskedValue);
        }
    }

    [Fact]
    public void CompareVersions_WhenSecretsAreUnchanged_ReportsNothing()
    {
        var stepId = Guid.NewGuid();

        var changes = _adapter.CompareVersions(
            BuildAutomation("same", "same", "same", stepId),
            BuildAutomation("same", "same", "same", stepId));

        changes.ShouldBeEmpty();
    }

    [Fact]
    public void CompareVersions_StillShowsNonSensitiveSettings()
    {
        var stepId = Guid.NewGuid();
        var from = BuildAutomation("same", "same", "same", stepId);
        var to = BuildAutomation("same", "same", "same", stepId);
        to.Steps[0].Settings["url"] = "https://changed.example.com";

        var change = _adapter.CompareVersions(from, to).ShouldHaveSingleItem();

        change.Path.ShouldBe($"Steps[{stepId}].Settings.url");
        change.OldValue.ShouldBe("https://example.com");
        change.NewValue.ShouldBe("https://changed.example.com");
    }
}
