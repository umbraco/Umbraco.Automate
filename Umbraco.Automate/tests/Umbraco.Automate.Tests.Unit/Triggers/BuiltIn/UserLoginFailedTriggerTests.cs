using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserLoginFailedTriggerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly UserLoginFailedTrigger _trigger;

    public UserLoginFailedTriggerTests()
    {
        _trigger = new UserLoginFailedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _userService.Object,
            NullLogger<UserLoginFailedTrigger>.Instance);
    }

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userLoginFailed");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Login Failed");

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var affectedKey = Guid.NewGuid();
        var notification = new UserLoginFailedNotification("10.0.0.1", affectedKey.ToString(), "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userLoginFailed");
        evt.Output.AffectedUserId.ShouldBe(affectedKey.ToString());
        evt.Output.PerformingUserId.ShouldBe("performing-1");
        evt.Output.IpAddress.ShouldBe("10.0.0.1");
        evt.IdempotencyKey.ShouldStartWith($"umbracoAutomate.userLoginFailed:{affectedKey}:t");
    }

    [Fact]
    public void MapEvent_NullAffectedUserId_StillProducesStableKey()
    {
        // A failed login for an unknown username has no affected user id — the mapper
        // must not throw and must produce a deterministic key.
        var notification = new UserLoginFailedNotification("10.0.0.1", affectedUserId: null!, "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldStartWith("umbracoAutomate.userLoginFailed:unknown:t");
        events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>()
            .Output.UserGroupKeys.ShouldBeEmpty();
    }
}
