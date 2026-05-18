using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MediaBatchDeletedTriggerTests
{
    private readonly MediaBatchDeletedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaBatchDeleted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Batch Deleted");

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(BatchTriggerOutput<MediaDeletedTriggerOutput>));

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
        // Umbraco fires the notification per-item, so the batch will usually contain one
        // item, but the trigger handles whatever the notification carries.
        var media = MediaSavedTriggerTests.CreateMedia(Guid.NewGuid(), "Image", "Image");

        var notification = new MediaDeletedNotification(media, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var batchEvent = events[0].ShouldBeOfType<TriggerEvent<BatchTriggerOutput<MediaDeletedTriggerOutput>>>();
        batchEvent.TriggerAlias.ShouldBe("umbracoAutomate.mediaBatchDeleted");
        batchEvent.Output.Count.ShouldBe(1);
        batchEvent.Output.Items[0].MediaName.ShouldBe("Image");
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var batch = new BatchTriggerOutput<MediaDeletedTriggerOutput>
        {
            Items = [new MediaDeletedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() }],
            Count = 1,
        };

        ((ITrigger)_trigger).CanHandle(batch, null).ShouldBeTrue();
    }
}
