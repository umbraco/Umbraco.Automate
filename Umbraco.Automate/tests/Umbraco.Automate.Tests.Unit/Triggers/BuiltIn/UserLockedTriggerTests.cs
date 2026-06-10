using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserLockedTriggerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly UserLockedTrigger _trigger;

    public UserLockedTriggerTests()
    {
        _trigger = new UserLockedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _userService.Object,
            NullLogger<UserLockedTrigger>.Instance);
    }

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userLocked");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Locked");

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var affectedKey = Guid.NewGuid();
        var notification = new UserLockedNotification("10.0.0.1", affectedKey.ToString(), "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userLocked");
        evt.Output.AffectedUserId.ShouldBe(affectedKey.ToString());
        evt.IdempotencyKey.ShouldStartWith($"umbracoAutomate.userLocked:{affectedKey}:t");
    }
}
