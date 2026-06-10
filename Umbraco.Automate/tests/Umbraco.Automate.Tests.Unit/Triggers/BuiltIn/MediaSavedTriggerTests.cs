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

public class MediaSavedTriggerTests
{
    private readonly MediaSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.mediaSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Media Saved");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(MediaSavedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(MediaSavedTriggerOutput));

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
        properties.Keys.ShouldContain("mediaName");
        properties.Keys.ShouldContain("mediaTypeAlias");
        properties.Keys.ShouldContain("isNew");
    }

    [Fact]
    public void MapEvent_ProducesEventPerSavedItem()
    {
        var media1 = CreateMedia(Guid.NewGuid(), "Image One", "Image", isNew: true);
        var media2 = CreateMedia(Guid.NewGuid(), "Image Two", "Image", isNew: false);

        var notification = new MediaSavedNotification(
            new[] { media1, media2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<MediaSavedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.mediaSaved");
        first.Output.MediaName.ShouldBe("Image One");
        first.Output.MediaTypeAlias.ShouldBe("Image");
        first.Output.IsNew.ShouldBeTrue();

        var second = events[1].ShouldBeOfType<TriggerEvent<MediaSavedTriggerOutput>>();
        second.Output.MediaName.ShouldBe("Image Two");
        second.Output.IsNew.ShouldBeFalse();
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
    public void MapEvent_SetsIdempotencyKey()
    {
        var mediaKey = Guid.NewGuid();
        var media = CreateMedia(mediaKey, "Image", "Image");

        var notification = new MediaSavedNotification(
            new[] { media },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.mediaSaved:{mediaKey}:");
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_MatchingMediaType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = typeKey };
        var settings = new MediaSavedTriggerSettings { MediaTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingMediaType_ReturnsFalse()
    {
        var output = new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaTypeKey = Guid.NewGuid() };
        var settings = new MediaSavedTriggerSettings { MediaTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    [Fact]
    public void CanHandle_MismatchedOutputType_ReturnsTrue()
    {
        // Defensive: a wrong output type shouldn't suppress automations.
        ((ITrigger)_trigger).CanHandle(new { }, new MediaSavedTriggerSettings()).ShouldBeTrue();
    }

    internal static IMedia CreateMedia(Guid key, string name, string mediaTypeAlias, bool isNew = false)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(mediaTypeAlias);

        // CreateDate == UpdateDate signals a newly-created item; diverged dates signal an edit.
        var createDate = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var updateDate = isNew ? createDate : createDate.AddSeconds(1);

        var media = new Mock<IMedia>();
        media.SetupGet(m => m.Key).Returns(key);
        media.SetupGet(m => m.Name).Returns(name);
        media.SetupGet(m => m.ContentType).Returns(contentType.Object);
        media.SetupGet(m => m.CreateDate).Returns(createDate);
        media.SetupGet(m => m.UpdateDate).Returns(updateDate);

        return media.Object;
    }
}
