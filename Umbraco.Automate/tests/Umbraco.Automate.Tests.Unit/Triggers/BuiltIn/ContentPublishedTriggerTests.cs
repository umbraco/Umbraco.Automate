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

public class ContentPublishedTriggerTests
{
    private readonly ContentPublishedTrigger _trigger = new(
        new TriggerInfrastructure(
            Mock.Of<IEditableModelResolver>(),
            Options.Create(new DeduplicationOptions { WindowMinutes = 5 })));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.contentPublished");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Content Published");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(ContentPublishedTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(ContentPublishedTriggerOutput));

    [Fact]
    public void HasSettingsSchema()
    {
        var schema = _trigger.GetSettingsSchema();
        schema.ShouldNotBeNull();
        schema.Fields.ShouldContain(f => f.PropertyName == "ContentTypeAlias");
    }

    [Fact]
    public void HasOutputProperties()
    {
        var props = _trigger.GetOutputSchema();
        props.Count.ShouldBeGreaterThan(0);
        props.ShouldContain(p => p.Name == "contentKey");
        props.ShouldContain(p => p.Name == "contentName");
        props.ShouldContain(p => p.Name == "contentTypeAlias");
    }

    [Fact]
    public void MapEvent_ProducesEventPerPublishedItem()
    {
        var content1 = CreateContent(Guid.NewGuid(), "Page One", "blogPost");
        var content2 = CreateContent(Guid.NewGuid(), "Page Two", "article");

        var notification = new ContentPublishedNotification(
            new[] { content1, content2 },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);

        var first = events[0].ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>();
        first.TriggerAlias.ShouldBe("umbracoAutomate.contentPublished");
        first.Output.ContentName.ShouldBe("Page One");
        first.Output.ContentTypeAlias.ShouldBe("blogPost");

        var second = events[1].ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>();
        second.Output.ContentName.ShouldBe("Page Two");
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
