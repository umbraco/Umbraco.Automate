using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserLoginSuccessTriggerTests
{
    private readonly UserLoginSuccessTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userLoginSuccess");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Login Success");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(UserAuthTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(UserAuthTriggerOutput));

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var notification = new UserLoginSuccessNotification("10.0.0.1", "affected-1", "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userLoginSuccess");
        evt.Output.AffectedUserId.ShouldBe("affected-1");
        evt.Output.PerformingUserId.ShouldBe("performing-1");
        evt.Output.IpAddress.ShouldBe("10.0.0.1");
        evt.Output.DateTimeUtc.ShouldBe(notification.DateTimeUtc);
        evt.IdempotencyKey.ShouldStartWith("umbracoAutomate.userLoginSuccess:affected-1:t");
    }
}
