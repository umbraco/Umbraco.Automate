using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ApplicationStartedTriggerTests
{
    private readonly Mock<IServerRoleAccessor> _serverRoleAccessor = new();
    private readonly ApplicationStartedTrigger _trigger;

    public ApplicationStartedTriggerTests()
    {
        _serverRoleAccessor.Setup(x => x.CurrentServerRole).Returns(ServerRole.Single);
        _trigger = new ApplicationStartedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _serverRoleAccessor.Object);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapEvent_PassesThroughIsRestarting(bool isRestarting)
    {
        var notification = new UmbracoApplicationStartedNotification(isRestarting);

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ApplicationStartedTriggerOutput>>()
            .Output;

        output.IsRestarting.ShouldBe(isRestarting);
    }

    [Theory]
    [InlineData(ServerRole.Single)]
    [InlineData(ServerRole.SchedulingPublisher)]
    [InlineData(ServerRole.Subscriber)]
    [InlineData(ServerRole.Unknown)]
    public void MapEvent_CapturesCurrentServerRole(ServerRole role)
    {
        _serverRoleAccessor.Setup(x => x.CurrentServerRole).Returns(role);

        var output = _trigger.MapEvent(new UmbracoApplicationStartedNotification(isRestarting: false))
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ApplicationStartedTriggerOutput>>()
            .Output;

        output.ServerRole.ShouldBe(role.ToString());
    }

    [Theory]
    [InlineData(ServerRole.Single)]
    [InlineData(ServerRole.SchedulingPublisher)]
    [InlineData(ServerRole.Subscriber)]
    [InlineData(ServerRole.Unknown)]
    public void CanHandle_MainServerFilterDisabled_AlwaysTrue(ServerRole role)
    {
        ITrigger trigger = _trigger;
        var output = new ApplicationStartedTriggerOutput { ServerRole = role.ToString() };

        trigger.CanHandle(output, null).ShouldBeTrue();
        trigger.CanHandle(output, new ApplicationStartedTriggerSettings { OnlyRunOnMainServer = false }).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ServerRole.Single, true)]
    [InlineData(ServerRole.SchedulingPublisher, true)]
    [InlineData(ServerRole.Unknown, true)]
    [InlineData(ServerRole.Subscriber, false)]
    public void CanHandle_MainServerFilterEnabled_SkipsSubscribers(ServerRole role, bool expected)
    {
        ITrigger trigger = _trigger;
        var output = new ApplicationStartedTriggerOutput { ServerRole = role.ToString() };
        var settings = new ApplicationStartedTriggerSettings { OnlyRunOnMainServer = true };

        trigger.CanHandle(output, settings).ShouldBe(expected);
    }
}
