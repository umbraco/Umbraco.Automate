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

public class ContentSavedTriggerTests
{
    private readonly ContentSavedTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentSaved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Saved");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ContentSavedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ContentSavedTriggerOutput));

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
    public void MapEvent_ProducesEventPerSavedItem()
    {
        var content1 = CreateContent(Guid.NewGuid(), "Page One", "blogPost");
        var content2 = CreateContent(Guid.NewGuid(), "Page Two", "article");

        var notification = new ContentSavedNotification(
            new[] { content1, content2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<ContentSavedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.contentSaved");
        first.Output.ContentName.ShouldBe("Page One");
        first.Output.ContentTypeAlias.ShouldBe("blogPost");

        var second = events[1].ShouldBeOfType<TriggerEvent<ContentSavedTriggerOutput>>();
        second.Output.ContentName.ShouldBe("Page Two");
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
    public void MapEvent_SetsIdempotencyKey()
    {
        var contentKey = Guid.NewGuid();
        var content = CreateContent(contentKey, "Page", "blogPost");

        var notification = new ContentSavedNotification(
            new[] { content },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);
        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldStartWith($"umbracoAutomate.contentSaved:{contentKey}:");
    }

    [Fact]
    public void CanHandle_NoSettings_ReturnsTrue()
    {
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };
        ((ITrigger)_trigger).CanHandle(output, null).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_MatchingContentType_ReturnsTrue()
    {
        var typeKey = Guid.NewGuid();
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = typeKey };
        var settings = new ContentSavedTriggerSettings { ContentTypes = typeKey.ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_NonMatchingContentType_ReturnsFalse()
    {
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentTypeKey = Guid.NewGuid() };
        var settings = new ContentSavedTriggerSettings { ContentTypes = Guid.NewGuid().ToString() };

        ((ITrigger)_trigger).CanHandle(output, settings).ShouldBeFalse();
    }

    [Fact]
    public void CanHandle_MismatchedOutputType_ReturnsTrue()
    {
        // Defensive: a wrong output type shouldn't suppress automations.
        ((ITrigger)_trigger).CanHandle(new { }, new ContentSavedTriggerSettings()).ShouldBeTrue();
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
