using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserDeletedTriggerTests
{
    private readonly UserDeletedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userDeleted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Deleted");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(UserDeletedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(UserDeletedTriggerOutput));

    [Fact]
    public void MapEvent_ProducesEventForDeletedUser()
    {
        var userKey = Guid.NewGuid();
        var user = UserSavedTriggerTests.CreateUser(userKey, "Alice", "alice", "alice@example.com");

        var notification = new UserDeletedNotification(user, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<UserDeletedTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.userDeleted");
        evt.Output.UserKey.ShouldBe(userKey);
        evt.Output.UserName.ShouldBe("Alice");
        evt.Output.Username.ShouldBe("alice");
        evt.Output.Email.ShouldBe("alice@example.com");
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var userKey = Guid.NewGuid();
        var user = UserSavedTriggerTests.CreateUser(userKey, "Alice", "alice", "alice@example.com");

        var notification = new UserDeletedNotification(user, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events[0].IdempotencyKey.ShouldBe($"umbracoAutomate.userDeleted:{userKey}");
    }

    [Fact]
    public void CanHandle_AlwaysReturnsTrue()
    {
        var output = new UserDeletedTriggerOutput { UserKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
        ((ITrigger)_trigger).CanHandle(output, new UserDeletedTriggerSettings()).ShouldBeTrue();
    }
}
