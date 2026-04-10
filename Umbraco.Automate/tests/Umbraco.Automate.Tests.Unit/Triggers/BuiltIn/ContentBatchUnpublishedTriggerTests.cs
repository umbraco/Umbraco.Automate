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

public class ContentBatchUnpublishedTriggerTests
{
    private readonly ContentBatchUnpublishedTrigger _trigger = new(
        new TriggerInfrastructure(
            Mock.Of<IEditableModelResolver>(),
            Options.Create(new DeduplicationOptions { WindowMinutes = 5 })));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentBatchUnpublished");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Batch Unpublished");

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<ContentUnpublishedTriggerOutput>));

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

        var notification = new ContentUnpublishedNotification(
            new[] { content1, content2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ContentUnpublishedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.contentBatchUnpublished");
        batchEvent.Output.Count.ShouldBe(2);
        batchEvent.Output.Items.Count.ShouldBe(2);
        batchEvent.Output.Items[0].ContentName.ShouldBe("Page One");
        batchEvent.Output.Items[1].ContentName.ShouldBe("Page Two");
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

    private static IContent CreateContent(Guid key, string name, string contentTypeAlias)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(contentTypeAlias);

        var content = new Mock<IContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.ContentType).Returns(contentType.Object);

        return content.Object;
    }
}
