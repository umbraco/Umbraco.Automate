using Json.Schema;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ElementPublishedTriggerTests
{
    private const string ElementName = "G2 review rating";
    private const string ElementTypeAlias = "schemaSnippet";

    private readonly ElementPublishedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    #region Happy path

    // STORY-9 AC5: Published
    [Fact]
    public void MapEvent_OnePublishedElement_ProducesOneEvent()
        => _trigger.MapEvent(new ElementPublishedNotification(CreateElement(Guid.NewGuid()), new EventMessages()))
            .ShouldHaveSingleItem();

    // STORY-9 AC5: Published (R17: once per element)
    [Fact]
    public void MapEvent_TwoPublishedElements_ProducesOneEventPerElement()
    {
        var notification = new ElementPublishedNotification(
            new[] { CreateElement(Guid.NewGuid()), CreateElement(Guid.NewGuid()) },
            new EventMessages());

        _trigger.MapEvent(notification).Count().ShouldBe(2);
    }

    // STORY-9 AC5: Published
    [Fact]
    public void MapEvent_PublishedElement_OutputHasTheElementKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key)).Output.ElementKey.ShouldBe(key);
    }

    // STORY-9 AC5: Published
    [Fact]
    public void MapEvent_PublishedElement_EventCarriesTheTriggerAlias()
        => MapSingle(CreateElement(Guid.NewGuid())).TriggerAlias.ShouldBe("umbracoAutomate.elementPublished");

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_PublishedElement_OutputHasTheElementName()
        => MapSingle(CreateElement(Guid.NewGuid())).Output.ElementName.ShouldBe(ElementName);

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_PublishedElement_OutputHasTheElementTypeKey()
    {
        var typeKey = Guid.NewGuid();

        MapSingle(CreateElement(Guid.NewGuid(), contentTypeKey: typeKey)).Output.ContentTypeKey.ShouldBe(typeKey);
    }

    // STORY-9 AC2 / SPEC "Output (per element)"
    [Fact]
    public void MapEvent_PublishedElement_OutputHasTheElementTypeAlias()
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

    // SPEC "Output (per element)": only Saved has isNew
    [Fact]
    public void OutputSchema_HasNoIsNew()
        => _trigger.GetOutputSchema()!.GetKeyword<PropertiesKeyword>()!.Properties.Keys.ShouldNotContain("isNew");

    // STORY-9 AC8: Type filter matches (R20)
    [Fact]
    public void CanHandle_MatchingElementType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new ElementPublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = typeKey };

        ((ITrigger)_trigger).CanHandle(output, new ElementPublishedTriggerSettings { ElementTypes = typeKey.ToString() }).ShouldBeTrue();
    }

    // STORY-10 AC3: Empty filter
    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(new ElementPublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() }, null)
            .ShouldBeTrue();

    // STORY-9 AC9: Invariant cultures (R23). The CMS reports "*" for an invariant element;
    // ContentCultureHelpers turns it into null, as for content (resolved in the decision log).
    [Fact]
    public void MapEvent_InvariantElement_CulturesIsNullAsForContent()
    {
        var key = Guid.NewGuid();
        var notification = new ElementPublishedNotification(
            CreateElement(key),
            new EventMessages(),
            publishedCultures: new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "*" } },
            unpublishedCultures: null);

        MapSingle(notification).Output.Cultures.ShouldBeNull();
    }

    // STORY-9 AC10: Variant cultures (R23)
    [Fact]
    public void MapEvent_VariantElementPublishedInOneCulture_CulturesIsThatCulture()
    {
        var key = Guid.NewGuid();
        var element = CreateElement(
            key,
            variations: ContentVariation.Culture,
            publishCultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(clean: new[] { "en-US", "da-DK" }));
        var notification = new ElementPublishedNotification(
            element,
            new EventMessages(),
            publishedCultures: new Dictionary<Guid, IReadOnlyCollection<string>> { [key] = new[] { "en-US" } },
            unpublishedCultures: null);

        MapSingle(notification).Output.Cultures.ShouldBe(new[] { "en-US" });
    }

    // STORY-9 AC10: Variant cultures (R23). No entry in the notification: the shared change-tracking fallback.
    [Fact]
    public void MapEvent_VariantElementMissingFromNotification_CulturesFallBackToChangedCultures()
    {
        var element = CreateElement(
            Guid.NewGuid(),
            variations: ContentVariation.Culture,
            publishCultureInfos: ContentPublishedTriggerTests.BuildCultureInfos(dirty: new[] { "en-US" }, clean: new[] { "da-DK" }));

        MapSingle(element).Output.Cultures.ShouldBe(new[] { "en-US" });
    }

    // STORY-9 AC5 (dedupe): keyed on the published version
    [Fact]
    public void MapEvent_IdempotencyKey_IsAliasElementKeyAndPublishedVersion()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey
            .ShouldBe($"umbracoAutomate.elementPublished:{key}:v12");
    }

    // STORY-9 AC5 (dedupe): a duplicate notification for the same publish collapses to one run
    [Fact]
    public void MapEvent_SamePublishTwice_SameIdempotencyKey()
    {
        var key = Guid.NewGuid();

        MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey
            .ShouldBe(MapSingle(CreateElement(key, publishedVersionId: 12)).IdempotencyKey);
    }

    // STORY-9 AC5 (dedupe): rapid successive publishes are separate runs
    [Fact]
    public void MapEvent_SuccessivePublishes_DistinctIdempotencyKeys()
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
        IElementScopedTriggerOutput output = new ElementPublishedTriggerOutput { ElementKey = key };

        output.GetElementKey().ShouldBe(key);
    }

    // STORY-10 AC1: Library group
    [Fact]
    public void HasTheAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.elementPublished");

    // STORY-10 AC1: Library group
    [Fact]
    public void HasTheName()
        => _trigger.Name.ShouldBe("Element Published");

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
        => _trigger.OutputType.ShouldBe(typeof(ElementPublishedTriggerOutput));

    // STORY-10 AC2: Element types only
    [Fact]
    public void HasTheSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ElementPublishedTriggerSettings));

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
        => new ElementPublishedTriggerSettings().OnAutomationOriginatedEvent
            .ShouldBe(nameof(AutomationOriginatedEventBehavior.SkipOnCycle));

    // STORY-10 AC4: Loop default (R22)
    [Fact]
    public void Settings_DefaultBehaviourIsSkipOnCycle()
        => ((IAutomationOriginatedEventBehavior)new ElementPublishedTriggerSettings()).OnAutomationOriginated
            .ShouldBe(AutomationOriginatedEventBehavior.SkipOnCycle);

    #endregion

    #region Sad path

    // STORY-9 AC11: Type filter doesn't match (R20)
    [Fact]
    public void CanHandle_NonMatchingElementType_ReturnsFalse()
    {
        var output = new ElementPublishedTriggerOutput { ElementKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };

        ((ITrigger)_trigger).CanHandle(output, new ElementPublishedTriggerSettings { ElementTypes = Guid.NewGuid().ToString() }).ShouldBeFalse();
    }

    // R17: nothing published, nothing to start
    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
        => _trigger.MapEvent(new ElementPublishedNotification(Array.Empty<IElement>(), new EventMessages())).ShouldBeEmpty();

    // Defensive, as for content: a wrong output type must not suppress automations.
    [Fact]
    public void CanHandle_MismatchedOutputType_ReturnsTrue()
        => ((ITrigger)_trigger).CanHandle(new { }, new ElementPublishedTriggerSettings()).ShouldBeTrue();

    #endregion

    private TriggerEvent<ElementPublishedTriggerOutput> MapSingle(IElement element)
        => MapSingle(new ElementPublishedNotification(element, new EventMessages()));

    private TriggerEvent<ElementPublishedTriggerOutput> MapSingle(ElementPublishedNotification notification)
        => _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ElementPublishedTriggerOutput>>();

    private static FieldAttribute ElementTypesField()
        => typeof(ElementPublishedTriggerSettings).GetProperty(nameof(ElementPublishedTriggerSettings.ElementTypes))!
            .GetCustomAttributes(typeof(FieldAttribute), false).Cast<FieldAttribute>().Single();

    /// <summary>
    /// A just-published <see cref="IElement"/> as the CMS hands it to <see cref="ElementPublishedNotification"/>.
    /// </summary>
    internal static IElement CreateElement(
        Guid key,
        Guid? contentTypeKey = null,
        ContentVariation variations = ContentVariation.Nothing,
        ContentCultureInfosCollection? publishCultureInfos = null,
        int publishedVersionId = 3)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Key).Returns(contentTypeKey ?? Guid.NewGuid());
        contentType.SetupGet(ct => ct.Alias).Returns(ElementTypeAlias);
        contentType.SetupGet(ct => ct.Variations).Returns(variations);

        var element = new Mock<IElement>();
        element.SetupGet(e => e.Key).Returns(key);
        element.SetupGet(e => e.Name).Returns(ElementName);
        element.SetupGet(e => e.ContentType).Returns(contentType.Object);
        element.SetupGet(e => e.PublishedVersionId).Returns(publishedVersionId);
        element.SetupGet(e => e.Published).Returns(true);
        element.SetupGet(e => e.PublishCultureInfos).Returns(publishCultureInfos);

        return element.Object;
    }
}
