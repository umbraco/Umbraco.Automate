using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserPasswordChangedTriggerTests
{
    private readonly UserPasswordChangedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userPasswordChanged");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Password Changed");

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var notification = new UserPasswordChangedNotification("10.0.0.1", "affected-1", "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userPasswordChanged");
        evt.Output.AffectedUserId.ShouldBe("affected-1");
        evt.Output.PerformingUserId.ShouldBe("performing-1");
        evt.IdempotencyKey.ShouldStartWith("umbracoAutomate.userPasswordChanged:affected-1:t");
    }
}
