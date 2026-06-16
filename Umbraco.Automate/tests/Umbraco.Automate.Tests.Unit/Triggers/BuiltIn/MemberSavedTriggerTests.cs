using Json.Schema;
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

public class MemberSavedTriggerTests
{
    private readonly Mock<IMemberService> _memberService = new();
    private readonly Mock<IMemberGroupService> _memberGroupService = new();
    private readonly MemberSavedTrigger _trigger;

    public MemberSavedTriggerTests()
    {
        _trigger = new MemberSavedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _memberService.Object,
            _memberGroupService.Object,
            NullLogger<MemberSavedTrigger>.Instance);
    }

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
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "MemberTypes");
        schema.Fields.ShouldContain(f => f.PropertyName == "MemberGroups");
    }

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
        properties.Keys.ShouldContain("memberGroupKeys");
        properties.Keys.ShouldContain("isNew");
    }

    [Fact]
    public void MapEvent_ProducesEventPerSavedItem()
    {
        StubMemberGroups();
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
    public void MapEvent_PopulatesMemberGroupKeys()
    {
        var groupKey1 = Guid.NewGuid();
        var groupKey2 = Guid.NewGuid();
        StubMemberGroups(roleIds: [1, 2], keys: [groupKey1, groupKey2]);

        var member = CreateMember(Guid.NewGuid(), "Alice", "alice", "alice@example.com", "Customer");
        var notification = new MemberSavedNotification(new[] { member }, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events[0].ShouldBeOfType<TriggerEvent<MemberSavedTriggerOutput>>()
            .Output.MemberGroupKeys.ShouldBe(new[] { groupKey1, groupKey2 }, ignoreOrder: true);
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
    public void MapEvent_LoginOnlyUpdate_ProducesNoEvents()
    {
        StubMemberGroups();
        var member = CreateMember(Guid.NewGuid(), "Alice", "alice", "alice@example.com", "Customer");

        // Umbraco flags login-only saves (last-login date / security stamp) in notification state.
        var notification = new MemberSavedNotification(new[] { member }, new EventMessages());
        notification.State[Umbraco.Cms.Core.Constants.Conventions.Member.LoginPropertiesOnlyStateKey] = true;

        var events = _trigger.MapEvent(notification).ToList();

        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        StubMemberGroups();
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
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new MemberSavedTriggerOutput { MemberKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_MatchingMemberType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new MemberSavedTriggerOutput { MemberKey = Guid.NewGuid(), MemberTypeKey = typeKey };
        var settings = new MemberSavedTriggerSettings { MemberTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingMemberType_ReturnsFalse()
    {
        var output = new MemberSavedTriggerOutput { MemberKey = Guid.NewGuid(), MemberTypeKey = Guid.NewGuid() };
        var settings = new MemberSavedTriggerSettings { MemberTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    [Fact]
    public void CanHandle_MatchingMemberGroup_ReturnsTrue()
    {
        var groupKey = Guid.NewGuid();
        var output = new MemberSavedTriggerOutput
        {
            MemberKey = Guid.NewGuid(),
            MemberGroupKeys = new[] { groupKey },
        };
        var settings = new MemberSavedTriggerSettings { MemberGroups = groupKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingMemberGroup_ReturnsFalse()
    {
        var output = new MemberSavedTriggerOutput
        {
            MemberKey = Guid.NewGuid(),
            MemberGroupKeys = new[] { Guid.NewGuid() },
        };
        var settings = new MemberSavedTriggerSettings { MemberGroups = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    [Fact]
    public void CanHandle_BothFiltersMustPass()
    {
        var typeKey = Guid.NewGuid();
        var groupKey = Guid.NewGuid();
        var output = new MemberSavedTriggerOutput
        {
            MemberKey = Guid.NewGuid(),
            MemberTypeKey = typeKey,
            MemberGroupKeys = new[] { Guid.NewGuid() }, // wrong group
        };
        var settings = new MemberSavedTriggerSettings
        {
            MemberTypes = typeKey.ToString(),
            MemberGroups = groupKey.ToString(),
        };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    private void StubMemberGroups(int[]? roleIds = null, Guid[]? keys = null)
    {
        roleIds ??= Array.Empty<int>();
        keys ??= Array.Empty<Guid>();

        _memberService
            .Setup(s => s.GetAllRolesIds(It.IsAny<int>()))
            .Returns(roleIds);

        var groups = keys.Select(k =>
        {
            var g = new Mock<IMemberGroup>();
            g.SetupGet(x => x.Key).Returns(k);
            return g.Object;
        });

        _memberGroupService
            .Setup(s => s.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(groups);
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
