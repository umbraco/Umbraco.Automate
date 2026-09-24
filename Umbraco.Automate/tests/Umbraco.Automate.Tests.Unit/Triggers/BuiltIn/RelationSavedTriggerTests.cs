using Json.Schema;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class RelationSavedTriggerTests
{
    private readonly RelationSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

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
    public void HasNoSettingsSchema()
        // RelationSavedTriggerSettings has no fields, so no schema is generated.
        => _trigger.GetSettingsSchema().ShouldBeNull();

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
        properties.Keys.ShouldContain("parentId");
        properties.Keys.ShouldContain("childObjectTypeKey");
        properties.Keys.ShouldContain("parentObjectTypeKey");
        properties.Keys.ShouldContain("comment");
        properties.Keys.ShouldContain("isNew");
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
        var parentObjectType = Guid.NewGuid();
        var childObjectType = Guid.NewGuid();
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
        output.ChildId.ShouldBe(20);
        output.ParentObjectTypeKey.ShouldBe(parentObjectType);
        output.ChildObjectTypeKey.ShouldBe(childObjectType);
        output.Comment.ShouldBe("a comment");
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
        events[0].IdempotencyKey.ShouldBe($"umbracoAutomate.relationSaved:{relationKey}");
    }

    [Fact]
    public void MapEvent_DuplicateSaveOfSameRelation_ProducesSameIdempotencyKey()
    {
        // Relations have no CMS version id to key off, so a re-saved relation (e.g. comment
        // edited) collapses to the same idempotency key as its prior save.
        var relationKey = Guid.NewGuid();
        var original = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: true);
        var edited = CreateRelation(relationKey, Guid.NewGuid(), 1, 2, isNew: false);

        var firstEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { original }, new EventMessages()))
            .ShouldHaveSingleItem();
        var secondEvent = _trigger.MapEvent(new RelationSavedNotification(new[] { edited }, new EventMessages()))
            .ShouldHaveSingleItem();

        firstEvent.IdempotencyKey.ShouldBe(secondEvent.IdempotencyKey);
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new RelationSavedTriggerOutput { Key = Guid.NewGuid(), RelationTypeKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
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
