using Json.Schema;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ElementUnpublishedTriggerTests
{
    private const string ElementName = "G2 review rating";
    private const string ElementTypeAlias = "schemaSnippet";

    private readonly ElementUnpublishedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    #region Happy path

    // STORY-9 AC6: Unpublished
    [Fact]
    public void MapEvent_OneUnpublishedElement_ProducesOneEvent()
        => _trigger.MapEvent(new ElementUnpublishedNotification(CreateElement(Guid.NewGuid()), new EventMessages()))
            .ShouldHaveSingleItem();

    // STORY-9 AC6: Unpublished (R17: once per element)
    [Fact]
    public void MapEvent_TwoUnpublishedElements_ProducesOneEventPerElement()
    {
        var notification = new ElementUnpublishedNotification(
            new[] { CreateElement(Guid.NewGuid()), CreateElement(Guid.NewGuid()) },
            new EventMessages());

        _trigger.MapEvent(notification).Count().ShouldBe(2);
    }

    // STORY-9 AC6: Unpublished
    [Fact]
    public void MapEvent_UnpublishedElement_OutputHasTheElementKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key)).Output.ElementKey.ShouldBe(key);
    }

    // STORY-9 AC6: Unpublished
    [Fact]
    public void MapEvent_UnpublishedElement_EventCarriesTheTriggerAlias()
        => MapSingle(CreateElement(Guid.NewGuid())).TriggerAlias.ShouldBe("umbracoAutomate.elementUnpublished");

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_UnpublishedElement_OutputHasTheElementName()
        => MapSingle(CreateElement(Guid.NewGuid())).Output.ElementName.ShouldBe(ElementName);

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_UnpublishedElement_OutputHasTheElementTypeKey()
    {
        var typeKey = Guid.NewGuid();

        MapSingle(CreateElement(Guid.NewGuid(), contentTypeKey: typeKey)).Output.ContentTypeKey.ShouldBe(typeKey);
    }

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_UnpublishedElement_OutputHasTheElementTypeAlias()
        => MapSingle(CreateElement(Guid.NewGuid())).Output.ContentTypeAlias.ShouldBe(ElementTypeAlias);

    // SPEC "Output (per element)": same as Saved minus isNew
    [Theory]
    [InlineData("elementKey")]
    [InlineData("elementName")]
    [InlineData("contentTypeKey")]
    [InlineData("contentTypeAlias")]
    [InlineData("cultures")]
    public void OutputSchema_HasProperty(string propertyName)
        => _trigger.GetOutputSchema()!.GetKeyword<PropertiesKeyword>()!.Properties.Keys.ShouldContain(propertyName);

    // STORY-9 AC16: Restore unpublishes (R19). The CMS unpublishes a published element while it is
    // still in the bin (UnpublishTrashedElementOnRestore runs before ParentId/Trashed change,
    // ElementEditingService.cs:409-418,463 at 18.1.0), so the element in the notification is trashed.
    [Fact]
    public void MapEvent_ElementUnpublishedOnRestore_ProducesAnEvent()
    {
        var notification = new ElementUnpublishedNotification(
            CreateElement(Guid.NewGuid(), trashed: true),
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>>());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // STORY-9 AC16: Restore unpublishes (R19). The R18 move/trash skip is Saved-only.
    [Fact]
    public void MapEvent_ElementUnpublishedWithParentIdAndTrashedDirty_StillProducesAnEvent()
    {
        var notification = new ElementUnpublishedNotification(
            CreateElement(Guid.NewGuid(), trashed: true, dirtyProperties: ["ParentId", "Trashed"]),
            new EventMessages());

        _trigger.MapEvent(notification).ShouldHaveSingleItem();
    }

    // STORY-9 AC16: Restore unpublishes (R19)
    [Fact]
    public void MapEvent_ElementUnpublishedOnRestore_OutputHasTheElementKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, trashed: true)).Output.ElementKey.ShouldBe(key);
    }

    // STORY-9 AC8: Type filter matches (R20)
    [Fact]
    public void CanHandle_MatchingElementType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new ElementUnpublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = typeKey };

        ((ITrigger)_trigger).CanHandle(output, new ElementUnpublishedTriggerSettings { ElementTypes = typeKey.ToString() }).ShouldBeTrue();
    }

    // STORY-10 AC3: Empty filter
    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(new ElementUnpublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() }, null)
            .ShouldBeTrue();

    // STORY-9 AC9: Invariant cultures (R23). The CMS reports "*" for an invariant element;
    // ContentCultureHelpers turns it into null, as for content (resolved in the decision log).
    [Fact]
    public void MapEvent_InvariantElement_CulturesIsNullAsForContent()
    {
        var key = Guid.NewGuid();
        var notification = new ElementUnpublishedNotification(
            CreateElement(key),
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "*" } });

        MapSingle(notification).Output.Cultures.ShouldBeNull();
    }

    // STORY-9 AC10: Variant cultures (R23)
    [Fact]
    public void MapEvent_VariantElementUnpublishedInOneCulture_CulturesIsThatCulture()
    {
        var key = Guid.NewGuid();
        var element = CreateElement(
            key,
            variations: ContentVariation.Culture,
            publishCultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(clean: new[] { "en-US", "da-DK" }));
        var notification = new ElementUnpublishedNotification(
            element,
            new EventMessages(),
            new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "da-DK" } });

        MapSingle(notification).Output.Cultures.ShouldBe(new[] { "da-DK" });
    }

    // STORY-9 AC10: Variant cultures (R23). No entry in the notification: all cultures that were published.
    [Fact]
    public void MapEvent_VariantElementMissingFromNotification_CulturesFallBackToPublishedCultures()
    {
        var element = CreateElement(
            Guid.NewGuid(),
            variations: ContentVariation.Culture,
            publishCultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(clean: new[] { "en-US", "da-DK" }));

        MapSingle(element).Output.Cultures.ShouldBe(new[] { "en-US", "da-DK" });
    }

    // STORY-9 AC6 (dedupe): keyed on the version that was live until the unpublish
    [Fact]
    public void MapEvent_IdempotencyKey_IsAliasElementKeyAndPublishedVersion()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey
            .ShouldBe($"umbracoAutomate.elementUnpublished:{key}:v12");
    }

    // STORY-9 AC6 (dedupe): a duplicate notification for the same unpublish collapses to one run
    [Fact]
    public void MapEvent_SameUnpublishTwice_SameIdempotencyKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey
            .ShouldBe(MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey);
    }

    // STORY-9 AC6 (dedupe): publish again, unpublish again: a separate run
    [Fact]
    public void MapEvent_UnpublishAfterRepublish_DistinctIdempotencyKeys()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey
            .ShouldNotBe(MapSingle(CreateElement(key, publishedVersionId: 13)).IdempotencyKey);
    }

    // STORY-9 AC17 (R21): the output carries the marker the dispatch authorizer checks for Read.
    [Fact]
    public void Output_IsElementScoped_WithTheElementKey()
    {
        var key = Guid.NewGuid();
        IElementScopedTriggerOutput output = new ElementUnpublishedTriggerOutput { ElementKey = key };

        output.GetElementKey().ShouldBe(key);
    }

    // STORY-10 AC1: Library group
    [Fact]
    public void HasTheAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.elementUnpublished");

    // STORY-10 AC1: Library group
    [Fact]
    public void HasTheName()
        => _trigger.Name.ShouldBe("Element Unpublished");

    // STORY-10 AC1: Library group
    [Fact]
    public void IsInTheLibraryGroup()
        => _trigger.Group.ShouldBe("Library");

    // SPEC "Triggers" table
    [Fact]
    public void HasTheGlobeIcon()
        => _trigger.Icon.ShouldBe("icon-globe");

    // STORY-10 AC5: No Library section
    [Fact]
    public void RequiresTheLibrarySection()
        => _trigger.RequiredSections.ShouldBe([Umbraco.Cms.Core.Constants.Applications.Library]);

    // SPEC "Output (per element)"
    [Fact]
    public void HasTheOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ElementUnpublishedTriggerOutput));

    // STORY-10 AC2: Element types only
    [Fact]
    public void HasTheSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ElementUnpublishedTriggerSettings));

    // STORY-10 AC2: Element types only
    [Fact]
    public void ElementTypesField_UsesTheDocumentTypePicker()
        => ElementTypesField().EditorUiAlias.ShouldBe(Umbraco.Automate.Core.Constants.EditorUiAliases.DocumentTypePicker);

    // STORY-10 AC2: Element types only
    [Fact]
    public void ElementTypesField_OnlyPicksElementTypes()
        => ElementTypesField().EditorConfig.ShouldContain("\"onlyPickElementTypes\", \"value\": true");

    // STORY-10 AC3: Empty filter
    [Fact]
    public void ElementTypesField_IsOptional()
        => _trigger.GetSettingsSchema()!.Fields.Single(f => f.PropertyName == "ElementTypes").IsRequired.ShouldBeFalse();

    // STORY-10 AC4: Loop default
    [Fact]
    public void Settings_LoopSettingDefaultsToSkipOnCycle()
        => new ElementUnpublishedTriggerSettings().OnAutomationOriginatedEvent
            .ShouldBe(nameof(AutomationOriginatedEventBehavior.SkipOnCycle));

    // STORY-10 AC4: Loop default (R22)
    [Fact]
    public void Settings_DefaultBehaviourIsSkipOnCycle()
        => ((IAutomationOriginatedEventBehavior)new ElementUnpublishedTriggerSettings()).OnAutomationOriginated
            .ShouldBe(AutomationOriginatedEventBehavior.SkipOnCycle);

    #endregion

    #region Sad path

    // STORY-9 AC11: Type filter doesn't match (R20)
    [Fact]
    public void CanHandle_NonMatchingElementType_ReturnsFalse()
    {
        var output = new ElementUnpublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };

        ((ITrigger)_trigger).CanHandle(output, new ElementUnpublishedTriggerSettings { ElementTypes = Guid.NewGuid().ToString() }).ShouldBeFalse();
    }

    // R17: nothing unpublished, nothing to start
    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
        => _trigger.MapEvent(new ElementUnpublishedNotification(Array.Empty<IElement>(), new EventMessages())).ShouldBeEmpty();

    // Defensive, as for content: a wrong output type must not suppress automations.
    [Fact]
    public void CanHandle_MismatchedOutputType_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(new { }, new ElementUnpublishedTriggerSettings()).ShouldBeTrue();

    #endregion

    private TriggerEvent<ElementUnpublishedTriggerOutput> MapSingle(IElement element)
        => MapSingle(new ElementUnpublishedNotification(element, new EventMessages()));

    private TriggerEvent<ElementUnpublishedTriggerOutput> MapSingle(ElementUnpublishedNotification notification)
        => _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ElementUnpublishedTriggerOutput>>();

    private static FieldAttribute ElementTypesField()
        => typeof(ElementUnpublishedTriggerSettings).GetProperty(nameof(ElementUnpublishedTriggerSettings.ElementTypes))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

    /// <summary>
    /// A just-unpublished <see cref="IElement"/> as the CMS hands it to <see cref="ElementUnpublishedNotification"/>.
    /// <paramref name="trashed"/> models the restore path, where the element is still in the bin.
    /// </summary>
    internal static IElement CreateElement(
        Guid key,
        Guid? contentTypeKey = null,
        ContentVariation variations = ContentVariation.Nothing,
        ContentCultureInfosCollection? publishCultureInfos = null,
        int publishedVersionId = 3,
        bool trashed = false,
        string[]? dirtyProperties = null)
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
        element.SetupGet(e => e.PublishedVersionId).Returns(publishedVersionId);
        element.SetupGet(e => e.Published).Returns(false);
        element.SetupGet(e => e.PublishCultureInfos).Returns(publishCultureInfos);
        element.SetupGet(e => e.Trashed).Returns(trashed);
        element.Setup(e => e.WasPropertyDirty(It.IsAny<string>())).Returns<string>(dirty.Contains);

        return element.Object;
    }
}
