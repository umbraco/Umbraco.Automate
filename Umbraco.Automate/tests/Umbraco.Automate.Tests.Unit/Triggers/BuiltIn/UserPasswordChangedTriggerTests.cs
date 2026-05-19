using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserPasswordChangedTriggerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly UserPasswordChangedTrigger _trigger;

    public UserPasswordChangedTriggerTests()
    {
        _trigger = new UserPasswordChangedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _userService.Object,
            NullLogger<UserPasswordChangedTrigger>.Instance);
    }

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userPasswordChanged");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Password Changed");

    [Fact]
    public void MapEvent_ProducesEventWithExpectedShape()
    {
        var affectedKey = Guid.NewGuid();
        var notification = new UserPasswordChangedNotification("10.0.0.1", affectedKey.ToString(), "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userPasswordChanged");
        evt.Output.AffectedUserId.ShouldBe(affectedKey.ToString());
        evt.Output.PerformingUserId.ShouldBe("performing-1");
        evt.IdempotencyKey.ShouldStartWith($"umbracoAutomate.userPasswordChanged:{affectedKey}:t");
    }
}
