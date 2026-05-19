using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserLoginSuccessTriggerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly UserLoginSuccessTrigger _trigger;

    public UserLoginSuccessTriggerTests()
    {
        _trigger = new UserLoginSuccessTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _userService.Object,
            NullLogger<UserLoginSuccessTrigger>.Instance);
    }

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
        var affectedKey = Guid.NewGuid();
        StubUser(affectedKey, groupKeys: Array.Empty<Guid>());
        var notification = new UserLoginSuccessNotification("10.0.0.1", affectedKey.ToString(), "performing-1");

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userLoginSuccess");
        evt.Output.AffectedUserId.ShouldBe(affectedKey.ToString());
        evt.Output.PerformingUserId.ShouldBe("performing-1");
        evt.Output.IpAddress.ShouldBe("10.0.0.1");
        evt.Output.DateTimeUtc.ShouldBe(notification.DateTimeUtc);
        evt.IdempotencyKey.ShouldStartWith($"umbracoAutomate.userLoginSuccess:{affectedKey}:t");
    }

    [Fact]
    public void MapEvent_PopulatesUserGroupKeys()
    {
        var affectedKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        StubUser(affectedKey, groupKeys: [groupKey]);

        var notification = new UserLoginSuccessNotification("10.0.0.1", affectedKey.ToString(), "performing-1");
        var events = _trigger.MapEvent(notification).ToList();

        events[0].ShouldBeOfType<TriggerEvent<UserAuthTriggerOutput>>()
            .Output.UserGroupKeys.ShouldBe(new[] { groupKey });
    }

    [Fact]
    public void CanHandle_NonMatchingUserGroup_ReturnsFalse()
    {
        var output = new UserAuthTriggerOutput
        {
            AffectedUserId = Guid.NewGuid().ToString(),
            UserGroupKeys = new[] { Guid.NewGuid() },
        };
        var settings = new UserAuthTriggerSettings { UserGroups = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    private void StubUser(Guid key, Guid[] groupKeys)
    {
        var groups = groupKeys.Select(k =>
        {
            var g = new Mock<IReadOnlyUserGroup>();
            g.SetupGet(x => x.Key).Returns(k);
            return g.Object;
        }).ToArray();

        var user = new Mock<IUser>();
        user.SetupGet(u => u.Key).Returns(key);
        user.SetupGet(u => u.Groups).Returns(groups);

        _userService.Setup(s => s.GetAsync(key)).ReturnsAsync(user.Object);
    }
}
