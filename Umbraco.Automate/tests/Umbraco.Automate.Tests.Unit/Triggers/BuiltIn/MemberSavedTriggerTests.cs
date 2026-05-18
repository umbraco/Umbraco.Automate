using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MemberSavedTriggerTests
{
    private readonly MemberSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.memberSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Member Saved");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(MemberSavedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(MemberSavedTriggerOutput));

    [Fact]
    public void HasOutputProperties()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("memberKey");
        properties.Keys.ShouldContain("memberName");
        properties.Keys.ShouldContain("username");
        properties.Keys.ShouldContain("email");
        properties.Keys.ShouldContain("memberTypeAlias");
        properties.Keys.ShouldContain("isNew");
    }

    [Fact]
    public void MapEvent_ProducesEventPerSavedItem()
    {
        var member1 = CreateMember(Guid.NewGuid(), "Alice", "alice", "alice@example.com", "Customer", isNew: true);
        var member2 = CreateMember(Guid.NewGuid(), "Bob", "bob", "bob@example.com", "Customer", isNew: false);

        var notification = new MemberSavedNotification(
            new[] { member1, member2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<MemberSavedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.memberSaved");
        first.Output.MemberName.ShouldBe("Alice");
        first.Output.Username.ShouldBe("alice");
        first.Output.Email.ShouldBe("alice@example.com");
        first.Output.MemberTypeAlias.ShouldBe("Customer");
        first.Output.IsNew.ShouldBeTrue();

        var second = events[1].ShouldBeOfType<TriggerEvent<MemberSavedTriggerOutput>>();
        second.Output.MemberName.ShouldBe("Bob");
        second.Output.IsNew.ShouldBeFalse();
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new MemberSavedNotification(
            Array.Empty<IMember>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var memberKey = Guid.NewGuid();
        var member = CreateMember(memberKey, "Alice", "alice", "alice@example.com", "Customer");

        var notification = new MemberSavedNotification(
            new[] { member },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.memberSaved:{memberKey}:");
    }

    [Fact]
    public void CanHandle_AlwaysReturnsTrue()
    {
        // No filter for v1 — every saved member matches.
        var output = new MemberSavedTriggerOutput { MemberKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
        ((ITrigger)_trigger).CanHandle(output, new MemberSavedTriggerSettings()).ShouldBeTrue();
    }

    internal static IMember CreateMember(Guid key, string name, string username, string email, string memberTypeAlias, bool isNew = false)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(memberTypeAlias);
        contentType.SetupGet(ct => ct.Key).Returns(Guid.NewGuid());

        // CreateDate == UpdateDate signals a newly-created member; diverged dates signal an edit.
        var createDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var updateDate = isNew ? createDate : createDate.AddSeconds(1);

        var member = new Mock<IMember>();
        member.SetupGet(m => m.Key).Returns(key);
        member.SetupGet(m => m.Name).Returns(name);
        member.SetupGet(m => m.Username).Returns(username);
        member.SetupGet(m => m.Email).Returns(email);
        member.SetupGet(m => m.ContentType).Returns(contentType.Object);
        member.SetupGet(m => m.CreateDate).Returns(createDate);
        member.SetupGet(m => m.UpdateDate).Returns(updateDate);

        return member.Object;
    }
}
