using Json.Schema;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class RelationSavedTriggerTests
{
    private readonly Mock<IIdKeyMap> _idKeyMap = new();
    private readonly RelationSavedTrigger _trigger;

    public RelationSavedTriggerTests()
    {
        _idKeyMap
            .Setup(m => m.GetKeyForId(It.IsAny<int>(), It.IsAny<UmbracoObjectTypes>()))
            .Returns(Attempt<Guid>.Fail());

        _trigger = new RelationSavedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _idKeyMap.Object);
    }

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.relationSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Relation Saved");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(RelationSavedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(RelationSavedTriggerOutput));

    [Fact]
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "IncludeAutomaticRelations");
    }

    [Fact]
    public void HasOutputProperties()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("key");
        properties.Keys.ShouldContain("relationTypeKey");
        properties.Keys.ShouldContain("childId");
        properties.Keys.ShouldContain("childKey");
        properties.Keys.ShouldContain("parentId");
        properties.Keys.ShouldContain("parentKey");
        properties.Keys.ShouldContain("childObjectTypeKey");
        properties.Keys.ShouldContain("parentObjectTypeKey");
        properties.Keys.ShouldContain("comment");
        properties.Keys.ShouldContain("isNew");
        properties.Keys.ShouldContain("isAutomatic");
    }

    [Fact]
    public void MapEvent_ProducesEventPerSavedItem()
    {
        var relation1 = CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 1, 2, isNew: true);
        var relation2 = CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 3, 4, isNew: false);

        var notification = new RelationSavedNotification(
            new[] { relation1, relation2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.relationSaved");
        first.InitiatorType.ShouldBe(TriggerInitiatorType.System);
        first.Output.ParentId.ShouldBe(1);
        first.Output.ChildId.ShouldBe(2);
        first.Output.IsNew.ShouldBeTrue();

        var second = events[1].ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>();
        second.Output.ParentId.ShouldBe(3);
        second.Output.ChildId.ShouldBe(4);
        second.Output.IsNew.ShouldBeFalse();
    }

    [Fact]
    public void MapEvent_MapsAllOutputFields()
    {
        var key = Guid.NewGuid();
        var relationTypeKey = Guid.NewGuid();
        var parentKey = Guid.NewGuid();
        var childKey = Guid.NewGuid();
        var parentObjectType = Constants.ObjectTypes.Document;
        var childObjectType = Constants.ObjectTypes.Media;
        _idKeyMap.Setup(m => m.GetKeyForId(10, UmbracoObjectTypes.Document)).Returns(Attempt.Succeed(parentKey));
        _idKeyMap.Setup(m => m.GetKeyForId(20, UmbracoObjectTypes.Media)).Returns(Attempt.Succeed(childKey));

        var relation = CreateRelation(
            key,
            relationTypeKey,
            parentId: 10,
            childId: 20,
            parentObjectType: parentObjectType,
            childObjectType: childObjectType,
            comment: "a comment");

        var notification = new RelationSavedNotification(new[] { relation }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>()
            .Output;

        output.Key.ShouldBe(key);
        output.RelationTypeKey.ShouldBe(relationTypeKey);
        output.ParentId.ShouldBe(10);
        output.ParentKey.ShouldBe(parentKey);
        output.ChildId.ShouldBe(20);
        output.ChildKey.ShouldBe(childKey);
        output.ParentObjectTypeKey.ShouldBe(parentObjectType);
        output.ChildObjectTypeKey.ShouldBe(childObjectType);
        output.Comment.ShouldBe("a comment");
        output.IsAutomatic.ShouldBeFalse();
    }

    [Fact]
    public void MapEvent_UnresolvableEntities_LeavesKeysNull()
    {
        // Entity lookup fails (default mock setup) — keys should be null rather than throwing.
        var relation = CreateRelation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentId: 10,
            childId: 20,
            parentObjectType: Constants.ObjectTypes.Document,
            childObjectType: Constants.ObjectTypes.Document);

        var output = _trigger.MapEvent(new RelationSavedNotification(new[] { relation }, new EventMessages()))
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>()
            .Output;

        output.ParentKey.ShouldBeNull();
        output.ChildKey.ShouldBeNull();
    }

    [Fact]
    public void MapEvent_UnknownObjectType_LeavesKeysNullWithoutLookup()
    {
        var relation = CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 1, 2);

        var output = _trigger.MapEvent(new RelationSavedNotification(new[] { relation }, new EventMessages()))
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>()
            .Output;

        output.ParentKey.ShouldBeNull();
        output.ChildKey.ShouldBeNull();
        _idKeyMap.Verify(m => m.GetKeyForId(It.IsAny<int>(), It.IsAny<UmbracoObjectTypes>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapEvent_MapsIsAutomaticFromNotification(bool isAutomatic)
    {
        var relation = CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 1, 2);
        var notification = new RelationSavedNotification(new[] { relation }, new EventMessages())
        {
            IsAutomatic = isAutomatic,
        };

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationSavedTriggerOutput>>()
            .Output;

        output.IsAutomatic.ShouldBe(isAutomatic);
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new RelationSavedNotification(
            Array.Empty<IRelation>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var relationKey = Guid.NewGuid();
        var relation = CreateRelation(relationKey, Guid.NewGuid(), 1, 2);

        var notification = new RelationSavedNotification(new[] { relation }, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldBe(
            IdempotencyKeyFactory.ForVersionlessEntitySaveEvent("umbracoAutomate.relationSaved", relationKey, relation.UpdateDate));
    }

    [Fact]
    public void MapEvent_SameUpdateDate_ProducesSameIdempotencyKey()
    {
        // A duplicate notification for the same save collapses to the same key.
        var relationKey = Guid.NewGuid();
        var first = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: false);
        var duplicate = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: false);

        var firstEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { first }, new EventMessages()))
            .ShouldHaveSingleItem();
        var duplicateEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { duplicate }, new EventMessages()))
            .ShouldHaveSingleItem();

        duplicateEvent.IdempotencyKey.ShouldBe(firstEvent.IdempotencyKey);
    }

    [Fact]
    public void MapEvent_DifferentUpdateDate_ProducesDifferentIdempotencyKey()
    {
        // Relations have no CMS version id, so UpdateDate distinguishes legitimate re-saves
        // (e.g. comment edited) from the prior save.
        var relationKey = Guid.NewGuid();
        var original = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: true);
        var edited = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: false);

        var originalEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { original }, new EventMessages()))
            .ShouldHaveSingleItem();
        var editedEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { edited }, new EventMessages()))
            .ShouldHaveSingleItem();

        editedEvent.IdempotencyKey.ShouldNotBe(originalEvent.IdempotencyKey);
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new RelationSavedTriggerOutput { Key = Guid.NewGuid(), RelationTypeKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, null, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, null, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void CanHandle_RespectsIncludeAutomaticRelations(bool isAutomatic, bool? includeAutomatic, bool expected)
    {
        var output = new RelationSavedTriggerOutput { Key = Guid.NewGuid(), IsAutomatic = isAutomatic };
        var settings = includeAutomatic is null
            ? null
            : new RelationSavedTriggerSettings { IncludeAutomaticRelations = includeAutomatic.Value };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBe(expected);
    }

    internal static IRelation CreateRelation(
        Guid key,
        Guid relationTypeKey,
        int parentId,
        int childId,
        Guid? parentObjectType = null,
        Guid? childObjectType = null,
        string? comment = null,
        bool isNew = false)
    {
        var relationType = new Mock<IRelationType>();
        relationType.SetupGet(rt => rt.Key).Returns(relationTypeKey);

        // CreateDate == UpdateDate signals a newly-created relation; diverged dates signal an edit.
        var createDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var updateDate = isNew ? createDate : createDate.AddSeconds(1);

        var relation = new Mock<IRelation>();
        relation.SetupGet(r => r.Key).Returns(key);
        relation.SetupGet(r => r.RelationType).Returns(relationType.Object);
        relation.SetupGet(r => r.ParentId).Returns(parentId);
        relation.SetupGet(r => r.ChildId).Returns(childId);
        relation.SetupGet(r => r.ParentObjectType).Returns(parentObjectType ?? Guid.NewGuid());
        relation.SetupGet(r => r.ChildObjectType).Returns(childObjectType ?? Guid.NewGuid());
        relation.SetupGet(r => r.Comment).Returns(comment);
        relation.SetupGet(r => r.CreateDate).Returns(createDate);
        relation.SetupGet(r => r.UpdateDate).Returns(updateDate);

        return relation.Object;
    }
}
