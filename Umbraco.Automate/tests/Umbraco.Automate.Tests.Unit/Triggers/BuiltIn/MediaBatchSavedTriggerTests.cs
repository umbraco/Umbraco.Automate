using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MediaBatchSavedTriggerTests
{
    private readonly MediaBatchSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaBatchSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Batch Saved");

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<MediaSavedTriggerOutput>));

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
        var media1 = MediaSavedTriggerTests.CreateMedia(Guid.NewGuid(), "Image One", "Image");
        var media2 = MediaSavedTriggerTests.CreateMedia(Guid.NewGuid(), "Image Two", "Image");

        var notification = new MediaSavedNotification(
            new[] { media1, media2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<MediaSavedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.mediaBatchSaved");
        batchEvent.Output.Count.ShouldBe(2);
        batchEvent.Output.Items.Count.ShouldBe(2);
        batchEvent.Output.Items[0].MediaName.ShouldBe("Image One");
        batchEvent.Output.Items[1].MediaName.ShouldBe("Image Two");
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new MediaSavedNotification(
            Array.Empty<IMedia>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var batch = new BatchTriggerOutput<MediaSavedTriggerOutput>
        {
            Items = [new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() }],
            Count = 1,
        };

        ((ITrigger)_trigger).CanHandle(batch, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_AnyItemMatchingMediaType_ReturnsTrue()
    {
        var allowed = Guid.NewGuid();
        var batch = new BatchTriggerOutput<MediaSavedTriggerOutput>
        {
            Items =
            [
                new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() },
                new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = allowed },
            ],
            Count = 2,
        };
        var settings = new MediaSavedTriggerSettings { MediaTypes = allowed.ToString() };

        ((ITrigger)_trigger).CanHandle(batch, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NoItemsMatchingMediaType_ReturnsFalse()
    {
        var batch = new BatchTriggerOutput<MediaSavedTriggerOutput>
        {
            Items =
            [
                new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() },
                new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() },
            ],
            Count = 2,
        };
        var settings = new MediaSavedTriggerSettings { MediaTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(batch, settings).ShouldBeFalse();
    }
}
