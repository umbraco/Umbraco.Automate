// STORY-9, STORY-10. Pending until T17: delete the LIBRARY_ELEMENT_TRIGGERS guard and the Skip, then make it compile and pass.

#if LIBRARY_ELEMENT_TRIGGERS
using Json.Schema;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ElementSavedTriggerTests
{
    private const string ElementName = "G2 review rating";
    private const string ElementTypeAlias = "schemaSnippet";

    private static readonly DateTime CreateDate = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly ElementSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    #region Happy path

    // STORY-9 AC1: Saved starts a run
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_OneSavedElement_ProducesOneEvent()
    {
        var notification = new ElementSavedNotification(CreateElement(Guid.NewGuid()), new EventMessages());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // STORY-9 AC1: Saved starts a run
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_TwoSavedElements_ProducesOneEventPerElement()
    {
        var notification = new ElementSavedNotification(
            new[] { CreateElement(Guid.NewGuid()), CreateElement(Guid.NewGuid()) },
            new EventMessages());

        _trigger.MapEvent(notification).Count().ShouldBe(2);
    }

    // STORY-9 AC1: Saved starts a run
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElement_OutputHasTheElementKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key)).Output.ElementKey.ShouldBe(key);
    }

    // STORY-9 AC1: Saved starts a run
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElement_EventCarriesTheTriggerAlias()
        => MapSingle(CreateElement(Guid.NewGuid())).TriggerAlias.ShouldBe("umbracoAutomate.elementSaved");

    // STORY-9 AC2: Output
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElement_OutputHasTheElementName()
        => MapSingle(CreateElement(Guid.NewGuid())).Output.ElementName.ShouldBe(ElementName);

    // STORY-9 AC2: Output
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElement_OutputHasTheElementTypeKey()
    {
        var typeKey = Guid.NewGuid();

        MapSingle(CreateElement(Guid.NewGuid(), contentTypeKey: typeKey)).Output.ContentTypeKey.ShouldBe(typeKey);
    }

    // STORY-9 AC2: Output
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SavedElement_OutputHasTheElementTypeAlias()
        => MapSingle(CreateElement(Guid.NewGuid())).Output.ContentTypeAlias.ShouldBe(ElementTypeAlias);

    // STORY-9 AC2: Output
    [Theory(Skip = "Pending: T17")]
    [InlineData("elementKey")]
    [InlineData("elementName")]
    [InlineData("contentTypeKey")]
    [InlineData("contentTypeAlias")]
    [InlineData("cultures")]
    [InlineData("isNew")]
    public void OutputSchema_HasProperty(string propertyName)
        => _trigger.GetOutputSchema()!.GetKeyword<PropertiesKeyword>()!.Properties.Keys.ShouldContain(propertyName);

    // STORY-9 AC3: New element (R24)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_NewElement_IsNewIsTrue()
        => MapSingle(CreateElement(Guid.NewGuid(), isNew: true)).Output.IsNew.ShouldBeTrue();

    // STORY-9 AC3: New element (R18, R24). A new element's ParentId is set in its constructor
    // through the change-tracking setter (ContentBase.cs:35, TreeEntityBase.cs:75 at 18.1.0), so
    // the first save remembers ParentId as dirty. The move check must not swallow a create.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_NewElementWithParentIdDirty_StillProducesAnEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: true, dirtyProperties: ["ParentId"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // STORY-9 AC4: Edited element (R24)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_EditedElement_IsNewIsFalse()
        => MapSingle(CreateElement(Guid.NewGuid(), isNew: false)).Output.IsNew.ShouldBeFalse();

    // STORY-9 AC4: Edited element. An edit that only changed property values isn't a move.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_EditedElementWithOnlyValuesDirty_ProducesAnEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["UpdateDate", "Name"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // STORY-9 AC8: Type filter matches (R20)
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_MatchingElementType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = typeKey };
        var settings = new ElementSavedTriggerSettings { ElementTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    // STORY-9 AC8: Type filter matches. The picker stores several types as a CSV.
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_ElementTypeAmongSeveral_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = typeKey };
        var settings = new ElementSavedTriggerSettings { ElementTypes = $"{Guid.NewGuid()},{typeKey}" };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    // STORY-10 AC3: Empty filter
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };

        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    // STORY-10 AC3: Empty filter
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_EmptyElementTypes_ReturnsTrue()
    {
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };
        var settings = new ElementSavedTriggerSettings { ElementTypes = string.Empty };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    // STORY-9 AC9: Invariant cultures (R23). AC9 says ["*"], but R23 says "as the content triggers
    // report them", and ContentCultureHelpers turns the CMS's "*" marker into null for invariant
    // content (ContentCultureHelpers.cs:23-24; ContentSavedTriggerTests.MapEvent_InvariantContent_CulturesIsNull).
    // This spec follows the shared helper. Decide before T17 and fix STORIES or this test.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_InvariantElement_CulturesIsNullAsForContent()
    {
        var key = Guid.NewGuid();
        var notification = new ElementSavedNotification(
            CreateElement(key),
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "*" } });

        MapSingle(notification).Output.Cultures.ShouldBeNull();
    }

    // STORY-9 AC10: Variant cultures (R23)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_VariantElement_CulturesComeFromTheNotification()
    {
        var key = Guid.NewGuid();
        var element = CreateElement(
            key,
            variations: ContentVariation.Culture,
            cultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(dirty: new[] { "da-DK" }));
        var notification = new ElementSavedNotification(
            element,
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "en-US" } });

        MapSingle(notification).Output.Cultures.ShouldBe(new[] { "en-US" });
    }

    // STORY-9 AC10: Variant cultures (R23). No entry in the notification: the shared change-tracking fallback.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_VariantElementMissingFromNotification_CulturesFallBackToChangedCultures()
    {
        var element = CreateElement(
            Guid.NewGuid(),
            variations: ContentVariation.Culture,
            cultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(dirty: new[] { "en-US" }, clean: new[] { "da-DK" }));

        MapSingle(element).Output.Cultures.ShouldBe(new[] { "en-US" });
    }

    // STORY-9 AC1 (dedupe): a duplicate notification for the same save collapses to one run.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_IdempotencyKey_StartsWithAliasAndElementKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key)).IdempotencyKey.ShouldStartWith($"umbracoAutomate.elementSaved:{key}:");
    }

    // STORY-9 AC1 (dedupe): a duplicate notification for the same save collapses to one run.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SameSaveTwice_SameIdempotencyKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key)).IdempotencyKey
            .ShouldBe(MapSingle(CreateElement(key)).IdempotencyKey);
    }

    // STORY-9 AC1 (dedupe): draft saves reuse the VersionId, so UpdateDate keeps sequential saves apart.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SequentialSavesOfTheSameVersion_DistinctIdempotencyKeys()
    {
        var key = Guid.NewGuid();
        var first = CreateElement(key, isNew: false, updateDate: CreateDate.AddMinutes(1));
        var second = CreateElement(key, isNew: false, updateDate: CreateDate.AddMinutes(2));

        MapSingle(first).IdempotencyKey.ShouldNotBe(MapSingle(second).IdempotencyKey);
    }

    // STORY-9 AC17 (R21): the output carries the marker the dispatch authorizer checks for Read.
    [Fact(Skip = "Pending: T17")]
    public void Output_IsElementScoped_WithTheElementKey()
    {
        var key = Guid.NewGuid();
        IElementScopedTriggerOutput output = new ElementSavedTriggerOutput { ElementKey = key };

        output.GetElementKey().ShouldBe(key);
    }

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void HasTheAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.elementSaved");

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void HasTheName()
        => _trigger.Name.ShouldBe("Element Saved");

    // STORY-10 AC1: Library group
    [Fact(Skip = "Pending: T17")]
    public void IsInTheLibraryGroup()
        => _trigger.Group.ShouldBe("Library");

    // SPEC "Triggers" table
    [Fact(Skip = "Pending: T17")]
    public void HasTheSaveIcon()
        => _trigger.Icon.ShouldBe("icon-save");

    // STORY-10 AC5: No Library section
    [Fact(Skip = "Pending: T17")]
    public void RequiresTheLibrarySection()
        => _trigger.RequiredSections.ShouldBe([Umbraco.Cms.Core.Constants.Applications.Library]);

    // STORY-9 AC2: Output
    [Fact(Skip = "Pending: T17")]
    public void HasTheOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ElementSavedTriggerOutput));

    // STORY-10 AC2: Element types only
    [Fact(Skip = "Pending: T17")]
    public void HasTheSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ElementSavedTriggerSettings));

    // STORY-10 AC2: Element types only
    [Fact(Skip = "Pending: T17")]
    public void SettingsSchema_HasElementTypes()
        => _trigger.GetSettingsSchema()!.Fields.ShouldContain(f => f.PropertyName == "ElementTypes");

    // STORY-10 AC2: Element types only
    [Fact(Skip = "Pending: T17")]
    public void ElementTypesField_UsesTheDocumentTypePicker()
        => ElementTypesField().EditorUiAlias.ShouldBe(Umbraco.Automate.Core.Constants.EditorUiAliases.DocumentTypePicker);

    // STORY-10 AC2: Element types only
    [Fact(Skip = "Pending: T17")]
    public void ElementTypesField_OnlyPicksElementTypes()
        => ElementTypesField().EditorConfig.ShouldContain("\"onlyPickElementTypes\", \"value\": true");

    // STORY-10 AC3: Empty filter
    [Fact(Skip = "Pending: T17")]
    public void ElementTypesField_IsOptional()
        => _trigger.GetSettingsSchema()!.Fields.Single(f => f.PropertyName == "ElementTypes").IsRequired.ShouldBeFalse();

    // STORY-10 AC4: Loop default
    [Fact(Skip = "Pending: T17")]
    public void Settings_LoopSettingDefaultsToSkipOnCycle()
        => new ElementSavedTriggerSettings().OnAutomationOriginatedEvent
            .ShouldBe(nameof(AutomationOriginatedEventBehavior.SkipOnCycle));

    // STORY-10 AC4: Loop default (R22)
    [Fact(Skip = "Pending: T17")]
    public void Settings_DefaultBehaviourIsSkipOnCycle()
        => ((IAutomationOriginatedEventBehavior)new ElementSavedTriggerSettings()).OnAutomationOriginated
            .ShouldBe(AutomationOriginatedEventBehavior.SkipOnCycle);

    #endregion

    #region Sad path

    // STORY-9 AC11: Type filter doesn't match (R20)
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_NonMatchingElementType_ReturnsFalse()
    {
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };
        var settings = new ElementSavedTriggerSettings { ElementTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    // STORY-9 AC11: Type filter doesn't match. An element with no type can't match a set filter.
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_FilterSetAndNoElementType_ReturnsFalse()
    {
        var output = new ElementSavedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = null };
        var settings = new ElementSavedTriggerSettings { ElementTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    // STORY-9 AC13: Moved to another folder (R18)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SaveThatOnlyMovedTheElement_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["ParentId", "Path", "Level"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC14: Sent to the recycle bin (R18)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SaveThatTrashedTheElement_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: false, trashed: true, dirtyProperties: ["ParentId", "Trashed", "Path"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC14: Sent to the recycle bin (R18). Trashed alone is enough to skip.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SaveWithOnlyTrashedDirty_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: false, trashed: true, dirtyProperties: ["Trashed"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC15: Restored (R18)
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_SaveThatRestoredTheElement_ProducesNoEvent()
    {
        var notification = new ElementSavedNotification(
            CreateElement(Guid.NewGuid(), isNew: false, trashed: false, dirtyProperties: ["ParentId", "Trashed", "Path"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldBeEmpty();
    }

    // STORY-9 AC13: Moved to another folder (R18). Only the moved element is left out.
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_MovedAndEditedElementsSavedTogether_ProducesEventOnlyForTheEdit()
    {
        var editedKey = Guid.NewGuid();
        var notification = new ElementSavedNotification(
            new[]
            {
                CreateElement(Guid.NewGuid(), isNew: false, dirtyProperties: ["ParentId"]),
                CreateElement(editedKey, isNew: false),
            },
            new EventMessages());

        _trigger.MapEvent(notification)
            .Cast<TriggerEvent<ElementSavedTriggerOutput>>()
            .Select(e => e.Output.ElementKey)
            .ShouldBe([editedKey]);
    }

    // STORY-9 AC1: nothing saved, nothing to start
    [Fact(Skip = "Pending: T17")]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
        => _trigger.MapEvent(new ElementSavedNotification(Array.Empty<IElement>(), new EventMessages())).ShouldBeEmpty();

    // Defensive, as for content: a wrong output type must not suppress automations.
    [Fact(Skip = "Pending: T17")]
    public void CanHandle_MismatchedOutputType_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(new { }, new ElementSavedTriggerSettings()).ShouldBeTrue();

    // STORY-10 AC4: Loop default. An unreadable stored value falls back to the default.
    [Fact(Skip = "Pending: T17")]
    public void Settings_UnknownLoopValue_FallsBackToSkipOnCycle()
        => ((IAutomationOriginatedEventBehavior)new ElementSavedTriggerSettings { OnAutomationOriginatedEvent = "nonsense" })
            .OnAutomationOriginated.ShouldBe(AutomationOriginatedEventBehavior.SkipOnCycle);

    #endregion

    private TriggerEvent<ElementSavedTriggerOutput> MapSingle(IElement element)
        => MapSingle(new ElementSavedNotification(element, new EventMessages()));

    private TriggerEvent<ElementSavedTriggerOutput> MapSingle(ElementSavedNotification notification)
        => _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ElementSavedTriggerOutput>>();

    private static FieldAttribute ElementTypesField()
        => typeof(ElementSavedTriggerSettings).GetProperty(nameof(ElementSavedTriggerSettings.ElementTypes))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

    /// <summary>
    /// A saved <see cref="IElement"/> as the CMS hands it to <see cref="ElementSavedNotification"/>:
    /// persisted, with the properties that changed in the save remembered as dirty
    /// (<c>WasPropertyDirty</c>). <c>CreateDate == UpdateDate</c> marks a new element.
    /// </summary>
    internal static IElement CreateElement(
        Guid key,
        bool isNew = false,
        Guid? contentTypeKey = null,
        ContentVariation variations = ContentVariation.Nothing,
        ContentCultureInfosCollection? cultureInfos = null,
        bool trashed = false,
        string[]? dirtyProperties = null,
        DateTime? updateDate = null)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Key).Returns(contentTypeKey ?? Guid.NewGuid());
        contentType.SetupGet(ct => ct.Alias).Returns(ElementTypeAlias);
        contentType.SetupGet(ct => ct.Variations).Returns(variations);

        var dirty = new HashSet<string>(dirtyProperties ?? []);

        var element = new Mock<IElement>();
        element.SetupGet(e => e.Key).Returns(key);
        element.SetupGet(e => e.Name).Returns(ElementName);
        element.SetupGet(e => e.ContentType).Returns(contentType.Object);
        element.SetupGet(e => e.VersionId).Returns(7);
        element.SetupGet(e => e.CreateDate).Returns(CreateDate);
        element.SetupGet(e => e.UpdateDate).Returns(isNew ? CreateDate : updateDate ?? CreateDate.AddSeconds(1));
        element.SetupGet(e => e.CultureInfos).Returns(cultureInfos);
        element.SetupGet(e => e.Trashed).Returns(trashed);
        element.Setup(e => e.WasPropertyDirty(It.IsAny<string>())).Returns<string>(dirty.Contains);
        element.Setup(e => e.GetWereDirtyProperties()).Returns(dirty);

        return element.Object;
    }
}
#endif
