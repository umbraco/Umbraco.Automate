using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MemberDeletedTriggerTests
{
    private readonly MemberDeletedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.memberDeleted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Member Deleted");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(MemberDeletedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(MemberDeletedTriggerOutput));

    [Fact]
    public void MapEvent_ProducesEventForDeletedMember()
    {
        var memberKey = Guid.NewGuid();
        var member = MemberSavedTriggerTests.CreateMember(memberKey, "Alice", "alice", "alice@example.com", "Customer");

        var notification = new MemberDeletedNotification(member, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        var evt = events[0].ShouldBeOfType<TriggerEvent<MemberDeletedTriggerOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoAutomate.memberDeleted");
        evt.Output.MemberKey.ShouldBe(memberKey);
        evt.Output.MemberName.ShouldBe("Alice");
        evt.Output.Username.ShouldBe("alice");
        evt.Output.Email.ShouldBe("alice@example.com");
        evt.Output.MemberTypeAlias.ShouldBe("Customer");
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var memberKey = Guid.NewGuid();
        var member = MemberSavedTriggerTests.CreateMember(memberKey, "Alice", "alice", "alice@example.com", "Customer");

        var notification = new MemberDeletedNotification(member, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.memberDeleted:{memberKey}:v");
    }

    [Fact]
    public void CanHandle_AlwaysReturnsTrue()
    {
        var output = new MemberDeletedTriggerOutput { MemberKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
        ((ITrigger)_trigger).CanHandle(output, new MemberDeletedTriggerSettings()).ShouldBeTrue();
    }
}
