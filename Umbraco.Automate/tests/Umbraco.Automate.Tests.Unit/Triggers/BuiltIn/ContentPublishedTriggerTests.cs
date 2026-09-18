using Json.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ContentPublishedTriggerTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly ContentPublishedTrigger _trigger;

    public ContentPublishedTriggerTests()
    {
        _trigger = new ContentPublishedTrigger(
            new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
            _userService.Object,
            Mock.Of<ILogger<ContentPublishedTrigger>>());
    }

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
        schema.Fields.ShouldContain(f => f.PropertyName == "ContentTypes");
        schema.Fields.ShouldContain(f => f.PropertyName == "PublishedBy");
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

    [Fact]
    public void MapEvent_InvariantContent_CulturesIsNull()
    {
        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost");

        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBeNull();
    }

    [Fact]
    public void MapEvent_VariantContent_CulturesContainsChangedPublishedCultures()
    {
        // On a live instance the changed cultures surface: "en-US" was just published (dirty),
        // "fr-FR" was already published and unchanged. (When the instance is a clone, dirty
        // state is lost and the helper falls back to all published — see
        // ContentCultureCloneRegressionTests.)
        var publishCultureInfos = BuildCultureInfos(dirty: new[] { "en-US" }, clean: new[] { "fr-FR" });
        var content = CreateContent(
            Guid.NewGuid(),
            "Page",
            "blogPost",
            variations: ContentVariation.Culture,
            publishCultureInfos: publishCultureInfos);

        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "en-US" });
    }

    [Fact]
    public void MapEvent_VariantContent_PrefersNotificationPublishedCultures()
    {
        // The authoritative per-document delta the CMS now reports (PR #23313) must win over the
        // change-tracking heuristic: dirty tracking would say "en-US", the notification says otherwise.
        var key = Guid.NewGuid();
        var content = CreateContent(
            key,
            "Page",
            "blogPost",
            variations: ContentVariation.Culture,
            publishCultureInfos: BuildCultureInfos(dirty: new[] { "en-US" }));

        var publishedCultures = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [key] = new[] { "da-DK", "de-DE" },
        };
        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages(),
            includeDescendants: false,
            publishedCultures: publishedCultures,
            unpublishedCultures: null);

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "da-DK", "de-DE" });
    }

    [Fact]
    public void MapEvent_VariantContent_FallsBackToChangeTracking_WhenNotificationHasNoEntryForItem()
    {
        // A descendant re-published as a side effect of publishing an ancestor is omitted from the
        // CMS map (umbraco/Umbraco-CMS#23288). The helper must then fall back to change tracking.
        var key = Guid.NewGuid();
        var content = CreateContent(
            key,
            "Page",
            "blogPost",
            variations: ContentVariation.Culture,
            publishCultureInfos: BuildCultureInfos(dirty: new[] { "en-US" }, clean: new[] { "fr-FR" }));

        // populated, but only for some OTHER document
        var publishedCultures = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Guid.NewGuid()] = new[] { "da-DK" },
        };
        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages(),
            includeDescendants: true,
            publishedCultures: publishedCultures,
            unpublishedCultures: null);

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "en-US" });
    }

    [Fact]
    public void MapEvent_InvariantContent_IgnoresNotificationCultures()
    {
        // The CMS reports invariant content as the "*" marker; Automate's contract keeps invariant as null.
        var key = Guid.NewGuid();
        var content = CreateContent(key, "Page", "blogPost");

        var publishedCultures = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [key] = new[] { "*" },
        };
        var notification = new ContentPublishedNotification(
            new[] { content },
            new EventMessages(),
            includeDescendants: false,
            publishedCultures: publishedCultures,
            unpublishedCultures: null);

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBeNull();
    }

    [Fact]
    public void MapEvent_BackofficePublisher_ClassifiedAsUser()
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(UserKind.Default);
        _userService.Setup(s => s.GetUserById(7)).Returns(user.Object);

        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost", publisherId: 7);
        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.PublisherId.ShouldBe(7);
        output.PublisherKind.ShouldBe(ContentPublisherKind.User);
    }

    [Fact]
    public void MapEvent_ApiUserPublisher_ClassifiedAsApi()
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(UserKind.Api);
        _userService.Setup(s => s.GetUserById(9)).Returns(user.Object);

        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost", publisherId: 9);
        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.PublisherKind.ShouldBe(ContentPublisherKind.Api);
    }

    [Fact]
    public void MapEvent_SuperUserPublisher_ClassifiedAsSystemWithoutLookup()
    {
        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost", publisherId: -1);
        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.PublisherKind.ShouldBe(ContentPublisherKind.System);
        _userService.Verify(s => s.GetUserById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void MapEvent_UnresolvablePublisher_KindIsNull()
    {
        _userService.Setup(s => s.GetUserById(99)).Returns((IUser?)null);

        var content = CreateContent(Guid.NewGuid(), "Page", "blogPost", publisherId: 99);
        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _trigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.PublisherId.ShouldBe(99);
        output.PublisherKind.ShouldBeNull();
    }

    [Fact]
    public void MapEvent_SharedPublisher_ResolvesOncePerNotification()
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(UserKind.Default);
        _userService.Setup(s => s.GetUserById(7)).Returns(user.Object);

        var notification = new ContentPublishedNotification(
            new[]
            {
                CreateContent(Guid.NewGuid(), "Page One", "blogPost", publisherId: 7),
                CreateContent(Guid.NewGuid(), "Page Two", "article", publisherId: 7),
            },
            new EventMessages());

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(2);
        _userService.Verify(s => s.GetUserById(7), Times.Once);
    }

    internal static IContent CreateContent(
        Guid key,
        string name,
        string contentTypeAlias,
        ContentVariation variations = ContentVariation.Nothing,
        ContentCultureInfosCollection? publishCultureInfos = null,
        ContentCultureInfosCollection? cultureInfos = null,
        int? publisherId = null)
    {
        var contentType = new Mock<ISimpleContentType>();
        contentType.SetupGet(ct => ct.Alias).Returns(contentTypeAlias);
        contentType.SetupGet(ct => ct.Variations).Returns(variations);

        var content = new Mock<IContent>();
        content.SetupGet(c => c.Key).Returns(key);
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.ContentType).Returns(contentType.Object);
        content.SetupGet(c => c.PublishCultureInfos).Returns(publishCultureInfos);
        content.SetupGet(c => c.CultureInfos).Returns(cultureInfos);
        content.SetupGet(c => c.PublisherId).Returns(publisherId);

        return content.Object;
    }

    /// <summary>
    /// Builds a <see cref="ContentCultureInfosCollection"/> mimicking a live post-commit
    /// instance: entries in <paramref name="dirty"/> have a Name set then
    /// <c>ResetDirtyProperties(true)</c> called (so <c>WasDirty()</c> is true = changed in this
    /// event); entries in <paramref name="clean"/> are untouched (so <c>WasDirty()</c> is false).
    /// The published helper reports the changed cultures on a live instance; on a cloned
    /// instance dirty state is lost and it falls back to all published (see issue #113 and
    /// <c>ContentCultureCloneRegressionTests</c>).
    /// </summary>
    internal static ContentCultureInfosCollection BuildCultureInfos(
        IEnumerable<string>? dirty = null,
        IEnumerable<string>? clean = null)
    {
        var collection = new ContentCultureInfosCollection();

        foreach (var culture in dirty ?? Array.Empty<string>())
        {
            var info = new ContentCultureInfos(culture) { Name = $"Name {culture}" };
            info.ResetDirtyProperties();
            collection.Add(info);
        }

        foreach (var culture in clean ?? Array.Empty<string>())
        {
            collection.Add(new ContentCultureInfos(culture));
        }

        return collection;
    }
}
