using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MemberDeletedTriggerTests
{
    private readonly Mock<IMemberService> _memberService = new();
    private readonly Mock<IMemberGroupService> _memberGroupService = new();
    private readonly MemberDeletedTrigger _trigger;

    public MemberDeletedTriggerTests()
    {
        _memberService.Setup(s => s.GetAllRolesIds(It.IsAny<int>())).Returns(Array.Empty<int>());
        _memberGroupService.Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(Array.Empty<IMemberGroup>());

        _trigger = new MemberDeletedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _memberService.Object,
            _memberGroupService.Object,
            NullLogger<MemberDeletedTrigger>.Instance);
    }

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
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "MemberTypes");
        schema.Fields.ShouldContain(f => f.PropertyName == "MemberGroups");
    }

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
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new MemberDeletedTriggerOutput { MemberKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_BothFiltersMustPass()
    {
        var typeKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var output = new MemberDeletedTriggerOutput
        {
            MemberKey = Guid.NewGuid(),
            MemberTypeKey = typeKey,
            MemberGroupKeys = new[] { groupKey },
        };
        var settings = new MemberDeletedTriggerSettings
        {
            MemberTypes = typeKey.ToString(),
            MemberGroups = groupKey.ToString(),
        };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }
}
