using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserLockedTriggerTests
{
    private readonly UserLockedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userLocked");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Locked");

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var notification = new UserLockedNotification("10.0.0.1", "affected-1", "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userLocked");
        evt.Output.AffectedUserId.ShouldBe("affected-1");
        evt.IdempotencyKey.ShouldStartWith("umbracoAutomate.userLocked:affected-1:t");
    }
}
