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

public class ContentBatchPublishedTriggerTests
{
    private readonly ContentBatchPublishedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentBatchPublished");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Batch Published");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ContentPublishedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<ContentPublishedTriggerOutput>));

    [Fact]
    public void HasOutputSchema()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("items");
        properties.Keys.ShouldContain("count");
    }

    [Fact]
    public void MapEvent_ProducesSingleEventWithAllItems()
    {
        var content1 = CreateContent(Guid.NewGuid(), "Page One", "blogPost");
        var content2 = CreateContent(Guid.NewGuid(), "Page Two", "article");
        var content3 = CreateContent(Guid.NewGuid(), "Page Three", "landingPage");

        var notification = new ContentPublishedNotification(
            new[] { content1, content2, content3 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.contentBatchPublished");
        batchEvent.Output.Count.ShouldBe(3);
        batchEvent.Output.Items.Count.ShouldBe(3);
        batchEvent.Output.Items[0].ContentName.ShouldBe("Page One");
        batchEvent.Output.Items[1].ContentName.ShouldBe("Page Two");
        batchEvent.Output.Items[2].ContentName.ShouldBe("Page Three");
    }

    [Fact]
    public void MapEvent_SingleItem_ProducesSingleEvent()
    {
        var content = CreateContent(Guid.NewGuid(), "Single Page", "blogPost");

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>>();
        batchEvent.Output.Count.ShouldBe(1);
        batchEvent.Output.Items[0].ContentName.ShouldBe("Single Page");
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new ContentPublishedNotification(
            Array.Empty<IContent>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void MapEvent_PreservesContentMetadata()
    {
        var key = Guid.NewGuid();
        var contentTypeKey = Guid.NewGuid();
        var content = CreateContent(key, "Test Page", "blogPost", contentTypeKey);

        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        var item = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ContentPublishedTriggerOutput>>>()
            .Output.Items[0];

        item.ContentKey.ShouldBe(key);
        item.ContentName.ShouldBe("Test Page");
        item.ContentTypeAlias.ShouldBe("blogPost");
        item.ContentTypeKey.ShouldBe(contentTypeKey);
    }

    private static IContent CreateContent(Guid key, string name, string contentTypeAlias, Guid? contentTypeKey = null)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(contentTypeAlias);
        contentType.SetupGet(ct => ct.Key).Returns(contentTypeKey ?? Guid.NewGuid());

        var content = new Mock<IContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.ContentType).Returns(contentType.Object);

        return content.Object;
    }
}
