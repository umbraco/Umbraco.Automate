using Json.Schema;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ContentUnpublishedTriggerTests
{
    private readonly ContentUnpublishedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentUnpublished");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Unpublished");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ContentUnpublishedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ContentUnpublishedTriggerOutput));

    [Fact]
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "ContentTypes");
    }

    [Fact]
    public void HasOutputProperties()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("contentKey");
        properties.Keys.ShouldContain("contentName");
        properties.Keys.ShouldContain("contentTypeAlias");
    }

    [Fact]
    public void MapEvent_ProducesEventPerUnpublishedItem()
    {
        var content1 = CreateContent(Guid.NewGuid(), "Page One", "blogPost");
        var content2 = CreateContent(Guid.NewGuid(), "Page Two", "article");

        var notification = new ContentUnpublishedNotification(
            new[] { content1, content2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<ContentUnpublishedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.contentUnpublished");
        first.Output.ContentName.ShouldBe("Page One");
        first.Output.ContentTypeAlias.ShouldBe("blogPost");

        var second = events[1].ShouldBeOfType<TriggerEvent<ContentUnpublishedTriggerOutput>>();
        second.Output.ContentName.ShouldBe("Page Two");
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new ContentUnpublishedNotification(
            Array.Empty<IContent>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var contentKey = Guid.NewGuid();
        var content = CreateContent(contentKey, "Page", "blogPost");

        var notification = new ContentUnpublishedNotification(
            new[] { content },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.contentUnpublished:{contentKey}:");
    }

    [Fact]
    public void MapEvent_InvariantContent_CulturesIsNull()
    {
        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost");

        var notification = new ContentUnpublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentUnpublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBeNull();
    }

    [Fact]
    public void MapEvent_VariantContent_CulturesContainsAllPreviouslyPublishedCultures()
    {
        // ContentUnpublishedNotification fires when the whole item is unpublished —
        // so all currently-published cultures (whether dirty or not) are being unpublished.
        var publishCultureInfos = ContentPublishedTriggerTests.BuildCultureInfos(
            clean: new[] { "en-US", "fr-FR" });
        var content = CreateContent(
            Guid.NewGuid(),
            "Page",
            "blogPost",
            variations: ContentVariation.Culture,
            publishCultureInfos: publishCultureInfos);

        var notification = new ContentUnpublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentUnpublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldNotBeNull();
        output.Cultures.ShouldBe(new[] { "en-US", "fr-FR" }, ignoreOrder: true);
    }

    private static IContent CreateContent(
        Guid key,
        string name,
        string contentTypeAlias,
        ContentVariation variations = ContentVariation.Nothing,
        ContentCultureInfosCollection? publishCultureInfos = null)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(contentTypeAlias);
        contentType.SetupGet(ct => ct.Variations).Returns(variations);

        var content = new Mock<IContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.ContentType).Returns(contentType.Object);
        content.SetupGet(c => c.PublishCultureInfos).Returns(publishCultureInfos);

        return content.Object;
    }
}
