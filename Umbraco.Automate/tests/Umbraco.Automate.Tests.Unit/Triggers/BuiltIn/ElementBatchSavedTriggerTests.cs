// STORY-9, STORY-10. Pending until T17: delete the LIBRARY_ELEMENT_TRIGGERS guard and the Skip, then make it compile and pass.

#if LIBRARY_ELEMENT_TRIGGERS
using Json.Schema;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ElementBatchSavedTriggerTests
{
    private readonly ElementBatchSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    #region Happy path

    // R17: once per operation for the batch triggers
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SeveralSavedElements_ProducesOneEvent()
    {
        var notification = new ElementSavedNotification(
            new[]
            {
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid()),
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid()),
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid()),
            },
            new EventMessages());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // R17: the run's items hold every element in the operation
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SeveralSavedElements_ItemsHoldEveryElement()
    {
        var keys = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var notification = new ElementSavedNotification(
            keys.Select(k => ElementSavedTriggerTests.CreateElement(k)).ToArray(),
            new EventMessages());

        MapSingle(notification).Output.Items.Select(i => i.ElementKey).ShouldBe(keys);
    }

    // R17
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SeveralSavedElements_CountMatchesItems()
    {
        var notification = new ElementSavedNotification(
            new[] { ElementSavedTriggerTests.CreateElement(Guid.NewGuid()), ElementSavedTriggerTests.CreateElement(Guid.NewGuid()) },
            new EventMessages());

        MapSingle(notification).Output.Count.ShouldBe(2);
    }

    // R17
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElements_EventCarriesTheTriggerAlias()
        => MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(Guid.NewGuid()), new EventMessages()))
            .TriggerAlias.ShouldBe("umbracoAutomate.elementBatchSaved");

    // STORY-9 AC3: New element (R24), per item
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_NewElement_ItemIsNewIsTrue()
        => MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: true), new EventMessages()))
            .Output.Items[0].IsNew.ShouldBeTrue();

    // STORY-9 AC4: Edited element (R24), per item
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_EditedElement_ItemIsNewIsFalse()
        => MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false), new EventMessages()))
            .Output.Items[0].IsNew.ShouldBeFalse();

    // STORY-9 AC3: New element. The move check must not drop a create (see ElementSavedTriggerTests).
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_NewElementWithParentIdDirty_IsKept()
    {
        var key = Guid.NewGuid();
        var notification = new ElementSavedNotification(
            ElementSavedTriggerTests.CreateElement(key, isNew: true, dirtyProperties: ["ParentId"]),
            new EventMessages());

        MapSingle(notification).Output.Items.Select(i => i.ElementKey).ShouldBe([key]);
    }

    // STORY-9 AC10: Variant cultures (R23), per item
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_VariantElement_ItemCulturesComeFromTheNotification()
    {
        var key = Guid.NewGuid();
        var notification = new ElementSavedNotification(
            new[] { ElementSavedTriggerTests.CreateElement(key, variations: ContentVariation.Culture) },
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "en-US" } });

        MapSingle(notification).Output.Items[0].Cultures.ShouldBe(new[] { "en-US" });
    }

    // STORY-9 AC9: Invariant cultures (R23). Follows the content contract (null); see the note in ElementSavedTriggerTests.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_InvariantElement_ItemCulturesIsNullAsForContent()
        => MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(Guid.NewGuid()), new EventMessages()))
            .Output.Items[0].Cultures.ShouldBeNull();

    // STORY-9 AC8: Type filter matches (R20)
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_AllItemsMatchingElementType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var batch = Batch(typeKey, typeKey);

        ((ITrigger)_trigger).CanHandle(batch, new ElementSavedTriggerSettings { ElementTypes = typeKey.ToString() }).ShouldBeTrue();
    }

    // STORY-9 AC12: Batch keeps matching types (R20). Only the "fires" half is testable on the
    // current API: CanHandle returns bool and nothing narrows a batch by settings
    // (TriggerEventHandler.cs:212,282; ContentBatchSavedTrigger fires the whole batch on any match).
    // "items hold only the snippet" needs a decision: see the report.
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_OneItemMatchingElementType_ReturnsTrue()
    {
        var snippetType = Guid.NewGuid();
        var batch = Batch(snippetType, Guid.NewGuid());

        ((ITrigger)_trigger).CanHandle(batch, new ElementSavedTriggerSettings { ElementTypes = snippetType.ToString() }).ShouldBeTrue();
    }

    // STORY-10 AC3: Empty filter
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_NoSettings_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(Batch(Guid.NewGuid()), null).ShouldBeTrue();

    // STORY-9 AC7 (dedupe): a duplicate notification for the same batch collapses to one run.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SameBatchTwice_SameIdempotencyKey()
    {
        var key = Guid.NewGuid();

        MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(key), new EventMessages())).IdempotencyKey
            .ShouldBe(MapSingle(new ElementSavedNotification(ElementSavedTriggerTests.CreateElement(key), new EventMessages())).IdempotencyKey);
    }

    // STORY-9 AC7 (dedupe): sequential saves of the same draft version are separate batches.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SequentialSavesOfTheSameVersion_DistinctIdempotencyKeys()
    {
        var key = Guid.NewGuid();
        var first = ElementSavedTriggerTests.CreateElement(key, updateDate: new DateTime(2026, 10, 1, 9, 1, 0, DateTimeKind.Utc));
        var second = ElementSavedTriggerTests.CreateElement(key, updateDate: new DateTime(2026, 10, 1, 9, 2, 0, DateTimeKind.Utc));

        MapSingle(new ElementSavedNotification(first, new EventMessages())).IdempotencyKey
            .ShouldNotBe(MapSingle(new ElementSavedNotification(second, new EventMessages())).IdempotencyKey);
    }

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void HasTheAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.elementBatchSaved");

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void HasTheName()
        => _trigger.Name.ShouldBe("Element Batch Saved");

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void IsInTheLibraryGroup()
        => _trigger.Group.ShouldBe("Library");

    // SPEC "Triggers" table
    [Fact(Skip = "Pending: T17")]
    public void HasThePluginIcon()
        => _trigger.Icon.ShouldBe("icon-plugin");

    // STORY-10 AC5: No Library section
    [Fact(Skip = "Pending: T17")]
    public void RequiresTheLibrarySection()
        => _trigger.RequiredSections.ShouldBe([Umbraco.Cms.Core.Constants.Applications.Library]);

    // STORY-10 AC2 / AC4: same settings as Element Saved (Element Types, loop default)
    [Fact(Skip = "Pending: T17")]
    public void SharesTheElementSavedSettings()
        => _trigger.SettingsType.ShouldBe(typeof(ElementSavedTriggerSettings));

    // STORY-9 AC19 (R21): a batch output the dispatch authorizer can narrow
    [Fact(Skip = "Pending: T17")]
    public void HasABatchOutputOfElementSavedItems()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<ElementSavedTriggerOutput>));

    // R17: the batch exposes items and count
    [Theory(Skip = "Pending: T17")]
    [InlineData("items")]
    [InlineData("count")]
    public void OutputSchema_HasProperty(string propertyName)
        => _trigger.GetOutputSchema()!.GetKeyword<PropertiesKeyword>()!.Properties.Keys.ShouldContain(propertyName);

    #endregion

    #region Sad path

    // STORY-9 AC12: Batch keeps matching types (R20): doesn't start when none match
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_NoItemsMatchingElementType_ReturnsFalse()
    {
        var batch = Batch(Guid.NewGuid(), Guid.NewGuid());

        ((ITrigger)_trigger).CanHandle(batch, new ElementSavedTriggerSettings { ElementTypes = Guid.NewGuid().ToString() }).ShouldBeFalse();
    }

    // STORY-9 AC13: Moved to another folder (R18). Moved elements are left out of the batch.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_MovedAndEditedElementsSavedTogether_ItemsHoldOnlyTheEdit()
    {
        var editedKey = Guid.NewGuid();
        var notification = new ElementSavedNotification(
            new[]
            {
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["ParentId"]),
                ElementSavedTriggerTests.CreateElement(editedKey, isNew: false),
            },
            new EventMessages());

        MapSingle(notification).Output.Items.Select(i => i.ElementKey).ShouldBe([editedKey]);
    }

    // STORY-9 AC13: Moved to another folder (R18). A batch of moves only starts nothing.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_OnlyMovedElements_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            new[]
            {
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["ParentId"]),
                ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["ParentId"]),
            },
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC14: Sent to the recycle bin (R18)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_OnlyTrashedElements_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false, trashed: true, dirtyProperties: ["ParentId", "Trashed"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC15: Restored (R18)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_OnlyRestoredElements_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            ElementSavedTriggerTests.CreateElement(Guid.NewGuid(), isNew: false, trashed: false, dirtyProperties: ["ParentId", "Trashed"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // R17: nothing saved, nothing to start
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
        => _trigger.MapEvent(new ElementSavedNotification(Array.Empty<IElement>(), new EventMessages())).ShouldBeEmpty();

    #endregion

    private TriggerEvent<BatchTriggerOutput<ElementSavedTriggerOutput>> MapSingle(ElementSavedNotification notification)
        => _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ElementSavedTriggerOutput>>>();

    private static BatchTriggerOutput<ElementSavedTriggerOutput> Batch(params Guid[] typeKeys)
    {
        var items = typeKeys
            .Select(t => new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = t })
            .ToList();

        return new BatchTriggerOutput<ElementSavedTriggerOutput> { Items = items, Count = items.Count };
    }
}
#endif
