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

public class MediaBatchTrashedTriggerTests
{
    private readonly MediaBatchTrashedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaBatchTrashed");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Batch Trashed");

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<MediaTrashedTriggerOutput>));

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

        var notification = new MediaMovedToRecycleBinNotification(
            new[]
            {
                new MoveToRecycleBinEventInfo<IMedia>(media1, "/-1/1001"),
                new MoveToRecycleBinEventInfo<IMedia>(media2, "/-1/1002"),
            },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<MediaTrashedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.mediaBatchTrashed");
        batchEvent.Output.Count.ShouldBe(2);
        batchEvent.Output.Items[0].MediaName.ShouldBe("Image One");
        batchEvent.Output.Items[1].OriginalPath.ShouldBe("/-1/1002");
    }

    [Fact]
    public void MapEvent_EmptyNotification_ProducesNoEvents()
    {
        var notification = new MediaMovedToRecycleBinNotification(
            Array.Empty<MoveToRecycleBinEventInfo<IMedia>>(),
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();
        events.ShouldBeEmpty();
    }

    [Fact]
    public void CanHandle_AnyItemMatchingMediaType_ReturnsTrue()
    {
        var allowed = Guid.NewGuid();
        var batch = new BatchTriggerOutput<MediaTrashedTriggerOutput>
        {
            Items =
            [
                new MediaTrashedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() },
                new MediaTrashedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = allowed },
            ],
            Count = 2,
        };
        var settings = new MediaTrashedTriggerSettings { MediaTypes = allowed.ToString() };

        ((ITrigger)_trigger).CanHandle(batch, settings).ShouldBeTrue();
    }
}
