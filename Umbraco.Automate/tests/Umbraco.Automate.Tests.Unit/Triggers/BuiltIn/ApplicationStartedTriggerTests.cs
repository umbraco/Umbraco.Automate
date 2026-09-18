using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ApplicationStartedTriggerTests
{
    private readonly ApplicationStartedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.applicationStarted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Application Started");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ApplicationStartedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ApplicationStartedTriggerOutput));

    [Fact]
    public void MapEvent_ProducesSingleEvent()
    {
        var notification = new UmbracoApplicationStartedNotification(isRestarting: false);

        var events = _trigger.MapEvent(notification).ToList();

        events.ShouldHaveSingleItem();
    }

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var notification = new UmbracoApplicationStartedNotification(isRestarting: false);

        var evt = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ApplicationStartedTriggerOutput>>();

        evt.TriggerAlias.ShouldBe("umbracoAutomate.applicationStarted");
        evt.InitiatorType.ShouldBe(TriggerInitiatorType.System);
    }

    [Fact]
    public void MapEvent_StampsStartedAtCloseToNow()
    {
        var notification = new UmbracoApplicationStartedNotification(isRestarting: false);
        var before = DateTimeOffset.UtcNow;

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ApplicationStartedTriggerOutput>>()
            .Output;

        var after = DateTimeOffset.UtcNow;
        output.StartedAt.ShouldBeInRange(before, after);
    }

    [Fact]
    public void MapEvent_OnRestart_StillProducesEvent()
    {
        var notification = new UmbracoApplicationStartedNotification(isRestarting: true);

        var events = _trigger.MapEvent(notification).ToList();

        events.ShouldHaveSingleItem();
    }
}
