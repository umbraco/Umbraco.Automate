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

public class ContentBatchSavedTriggerTests
{
    private readonly ContentBatchSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentBatchSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Batch Saved");

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<ContentSavedTriggerOutput>));

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

        var notification = new ContentSavedNotification(
            new[] { content1, content2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<ContentSavedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.contentBatchSaved");
        batchEvent.Output.Count.ShouldBe(2);
        batchEvent.Output.Items.Count.ShouldBe(2);
        batchEvent.Output.Items[0].ContentName.ShouldBe("Page One");
        batchEvent.Output.Items[1].ContentName.ShouldBe("Page Two");
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new ContentSavedNotification(
            Array.Empty<IContent>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var batch = new BatchTriggerOutput<ContentSavedTriggerOutput>
        {
            Items = [new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() }],
            Count = 1,
        };

        ((ITrigger)_trigger).CanHandle(batch, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_AnyItemMatchingContentType_ReturnsTrue()
    {
        var allowed = Guid.NewGuid();
        var batch = new BatchTriggerOutput<ContentSavedTriggerOutput>
        {
            Items =
            [
                new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() },
                new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = allowed },
            ],
            Count = 2,
        };
        var settings = new ContentSavedTriggerSettings { ContentTypes = allowed.ToString() };

        ((ITrigger)_trigger).CanHandle(batch, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NoItemsMatchingContentType_ReturnsFalse()
    {
        var batch = new BatchTriggerOutput<ContentSavedTriggerOutput>
        {
            Items =
            [
                new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() },
                new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() },
            ],
            Count = 2,
        };
        var settings = new ContentSavedTriggerSettings { ContentTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(batch, settings).ShouldBeFalse();
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
