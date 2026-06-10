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

public class MediaTrashedTriggerTests
{
    private readonly MediaTrashedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaTrashed");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Trashed");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(MediaTrashedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(MediaTrashedTriggerOutput));

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
        properties.Keys.ShouldContain("originalPath");
    }

    [Fact]
    public void MapEvent_ProducesEventPerTrashedItem()
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

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<MediaTrashedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.mediaTrashed");
        first.Output.MediaName.ShouldBe("Image One");
        first.Output.OriginalPath.ShouldBe("/-1/1001");
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
    public void MapEvent_SetsIdempotencyKey()
    {
        var mediaKey = Guid.NewGuid();
        var media = MediaSavedTriggerTests.CreateMedia(mediaKey, "Image", "Image");

        var notification = new MediaMovedToRecycleBinNotification(
            new[] { new MoveToRecycleBinEventInfo<IMedia>(media, "/-1/1001") },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.mediaTrashed:{mediaKey}:");
    }

    [Fact]
    public void CanHandle_MatchingMediaType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new MediaTrashedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = typeKey };
        var settings = new MediaTrashedTriggerSettings { MediaTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingMediaType_ReturnsFalse()
    {
        var output = new MediaTrashedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() };
        var settings = new MediaTrashedTriggerSettings { MediaTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }
}
