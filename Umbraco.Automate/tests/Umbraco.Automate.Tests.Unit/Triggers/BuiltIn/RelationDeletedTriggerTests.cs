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

public class RelationDeletedTriggerTests
{
    private readonly Mock<IIdKeyMap> _idKeyMap = new();
    private readonly RelationDeletedTrigger _trigger;

    public RelationDeletedTriggerTests()
    {
        _idKeyMap
            .Setup(m => m.GetKeyForId(It.IsAny<int>(), It.IsAny<UmbracoObjectTypes>()))
            .Returns(Attempt<Guid>.Fail());

        _trigger = new RelationDeletedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _idKeyMap.Object);
    }

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.relationDeleted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Relation Deleted");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(RelationDeletedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(RelationDeletedTriggerOutput));

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
        properties.Keys.ShouldContain("isAutomatic");
    }

    [Fact]
    public void MapEvent_ProducesEventPerDeletedItem()
    {
        var relation1 = RelationSavedTriggerTests.CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 1, 2);
        var relation2 = RelationSavedTriggerTests.CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 3, 4);

        var notification = new RelationDeletedNotification(
            new[] { relation1, relation2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<RelationDeletedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.relationDeleted");
        first.InitiatorType.ShouldBe(TriggerInitiatorType.System);
        first.Output.ParentId.ShouldBe(1);
        first.Output.ChildId.ShouldBe(2);

        var second = events[1].ShouldBeOfType<TriggerEvent<RelationDeletedTriggerOutput>>();
        second.Output.ParentId.ShouldBe(3);
        second.Output.ChildId.ShouldBe(4);
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

        var relation = RelationSavedTriggerTests.CreateRelation(
            key,
            relationTypeKey,
            parentId: 10,
            childId: 20,
            parentObjectType: parentObjectType,
            childObjectType: childObjectType,
            comment: "a comment");

        var notification = new RelationDeletedNotification(relation, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationDeletedTriggerOutput>>()
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
    public void MapEvent_DeletedEntities_LeavesKeysNull()
    {
        // The parent/child may already be gone (e.g. deleted along with the relation) —
        // keys should be null rather than throwing.
        var relation = RelationSavedTriggerTests.CreateRelation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            parentId: 10,
            childId: 20,
            parentObjectType: Constants.ObjectTypes.Document,
            childObjectType: Constants.ObjectTypes.Document);

        var output = _trigger.MapEvent(new RelationDeletedNotification(relation, new EventMessages()))
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationDeletedTriggerOutput>>()
            .Output;

        output.ParentKey.ShouldBeNull();
        output.ChildKey.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapEvent_MapsIsAutomaticFromNotification(bool isAutomatic)
    {
        var relation = RelationSavedTriggerTests.CreateRelation(Guid.NewGuid(), Guid.NewGuid(), 1, 2);
        var notification = new RelationDeletedNotification(relation, new EventMessages())
        {
            IsAutomatic = isAutomatic,
        };

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<RelationDeletedTriggerOutput>>()
            .Output;

        output.IsAutomatic.ShouldBe(isAutomatic);
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new RelationDeletedNotification(
            Array.Empty<IRelation>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var relationKey = Guid.NewGuid();
        var relation = RelationSavedTriggerTests.CreateRelation(relationKey, Guid.NewGuid(), 1, 2);

        var notification = new RelationDeletedNotification(relation, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldBe($"umbracoAutomate.relationDeleted:{relationKey}");
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new RelationDeletedTriggerOutput { Key = Guid.NewGuid(), RelationTypeKey = Guid.NewGuid() };
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
        var output = new RelationDeletedTriggerOutput { Key = Guid.NewGuid(), IsAutomatic = isAutomatic };
        var settings = includeAutomatic is null
            ? null
            : new RelationDeletedTriggerSettings { IncludeAutomaticRelations = includeAutomatic.Value };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBe(expected);
    }
}
