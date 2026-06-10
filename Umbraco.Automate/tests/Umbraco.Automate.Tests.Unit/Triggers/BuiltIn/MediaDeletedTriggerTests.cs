using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class MediaDeletedTriggerTests
{
    private readonly MediaDeletedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaDeleted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Deleted");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(MediaDeletedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(MediaDeletedTriggerOutput));

    [Fact]
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "MediaTypes");
    }

    [Fact]
    public void HasOutputProperties()
    {
        var schema = _trigger.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("mediaKey");
        properties.Keys.ShouldContain("mediaTypeAlias");
    }

    [Fact]
    public void MapEvent_ProducesOneEventForDeletedItem()
    {
        // Umbraco always raises MediaDeletedNotification one-per-item — even when deleting
        // a hierarchy it loops through descendants and fires the notification per descendant.
        var mediaKey = Guid.NewGuid();
        var media = MediaSavedTriggerTests.CreateMedia(mediaKey, "Image", "Image");

        var notification = new MediaDeletedNotification(media, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var first = events[0].ShouldBeOfType<TriggerEvent<MediaDeletedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.mediaDeleted");
        first.Output.MediaKey.ShouldBe(mediaKey);
        first.Output.MediaName.ShouldBe("Image");
        first.Output.MediaTypeAlias.ShouldBe("Image");
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var mediaKey = Guid.NewGuid();
        var media = MediaSavedTriggerTests.CreateMedia(mediaKey, "Image", "Image");

        var notification = new MediaDeletedNotification(media, new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.mediaDeleted:{mediaKey}:");
    }

    [Fact]
    public void CanHandle_MatchingMediaType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new MediaDeletedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = typeKey };
        var settings = new MediaDeletedTriggerSettings { MediaTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingMediaType_ReturnsFalse()
    {
        var output = new MediaDeletedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() };
        var settings = new MediaDeletedTriggerSettings { MediaTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }
}
