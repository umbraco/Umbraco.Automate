using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class UserSavedTriggerTests
{
    private readonly UserSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.userSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("User Saved");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(UserSavedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(UserSavedTriggerOutput));

    [Fact]
    public void HasOutputProperties()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("userKey");
        properties.Keys.ShouldContain("userName");
        properties.Keys.ShouldContain("username");
        properties.Keys.ShouldContain("email");
        properties.Keys.ShouldContain("isNew");
    }

    [Fact]
    public void MapEvent_ProducesEventPerSavedItem()
    {
        var user1 = CreateUser(Guid.NewGuid(), "Alice", "alice", "alice@example.com", isNew: true);
        var user2 = CreateUser(Guid.NewGuid(), "Bob", "bob", "bob@example.com", isNew: false);

        var notification = new UserSavedNotification(
            new[] { user1, user2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<UserSavedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.userSaved");
        first.Output.UserName.ShouldBe("Alice");
        first.Output.Username.ShouldBe("alice");
        first.Output.Email.ShouldBe("alice@example.com");
        first.Output.IsNew.ShouldBeTrue();

        var second = events[1].ShouldBeOfType<TriggerEvent<UserSavedTriggerOutput>>();
        second.Output.UserName.ShouldBe("Bob");
        second.Output.IsNew.ShouldBeFalse();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var userKey = Guid.NewGuid();
        var user = CreateUser(userKey, "Alice", "alice", "alice@example.com");

        var notification = new UserSavedNotification(new[] { user }, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.userSaved:{userKey}:u");
    }

    [Fact]
    public void CanHandle_AlwaysReturnsTrue()
    {
        var output = new UserSavedTriggerOutput { UserKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
        ((ITrigger)_trigger).CanHandle(output, new UserSavedTriggerSettings()).ShouldBeTrue();
    }

    internal static IUser CreateUser(Guid key, string name, string username, string email, bool isNew = false)
    {
        var createDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var updateDate = isNew ? createDate : createDate.AddSeconds(1);

        var user = new Mock<IUser>();
        user.SetupGet(u => u.Key).Returns(key);
        user.SetupGet(u => u.Name).Returns(name);
        user.SetupGet(u => u.Username).Returns(username);
        user.SetupGet(u => u.Email).Returns(email);
        user.SetupGet(u => u.CreateDate).Returns(createDate);
        user.SetupGet(u => u.UpdateDate).Returns(updateDate);

        return user.Object;
    }
}
