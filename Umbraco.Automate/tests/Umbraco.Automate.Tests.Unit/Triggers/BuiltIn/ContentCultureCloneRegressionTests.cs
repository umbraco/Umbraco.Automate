using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

/// <summary>
/// Regression guard for issue #113 — <c>${ trigger.cultures }</c> returning an empty array.
///
/// These tests use <b>real</b> Umbraco domain objects (no mocks for the content) so that the
/// production <see cref="IContentBase.DeepClone"/> semantics are exercised. Umbraco deep-clones
/// content across cache boundaries and when re-fetching descendants for republish, and
/// <see cref="ContentCultureInfosCollection.DeepClone"/> calls <c>ResetDirtyProperties(false)</c>
/// on every cloned entry — nulling both current and saved change sets, so <c>WasDirty()</c> is
/// permanently <c>false</c> on the clone.
///
/// The published helper must therefore never depend on dirty tracking alone: on a live instance
/// it reports the changed cultures, and on a cloned instance it falls back to all published
/// cultures (rather than an empty array).
/// </summary>
public class ContentCultureCloneRegressionTests
{
    private readonly ContentPublishedTrigger _publishedTrigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()),
        Mock.Of<IUserService>(),
        Mock.Of<ILogger<ContentPublishedTrigger>>());

    private readonly ContentSavedTrigger _savedTrigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    private static Content CreateVariantContent()
    {
        var contentType = new ContentType(Mock.Of<IShortStringHelper>(), -1)
        {
            Alias = "blogPost",
            Variations = ContentVariation.Culture,
        };

        return new Content("Page", -1, contentType) { Key = Guid.NewGuid() };
    }

    /// <summary>
    /// Variant content with two published cultures where only "en-US" changed in this event:
    /// "fr-FR" was published earlier (clean), "en-US" was just published (dirty). This lets the
    /// tests distinguish "changed cultures" (live) from "all published cultures" (clone fallback).
    /// </summary>
    private static Content CreatePublishedContentWithOneChangedCulture()
    {
        var content = CreateVariantContent();

        // "fr-FR" published in an earlier event, then dirty state cleared (not remembered).
        content.PublishCultureInfos!.AddOrUpdate("fr-FR", "Page fr-FR", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        content.ResetDirtyProperties(rememberDirty: false);

        // "en-US" published now; remember-dirty mimics the post-commit state.
        content.PublishCultureInfos!.AddOrUpdate("en-US", "Page en-US", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        content.ResetDirtyProperties(rememberDirty: true);

        return content;
    }

    /// <summary>
    /// Variant content with two available cultures where only "en-US" was edited in this save:
    /// "fr-FR" existed before (clean), "en-US" was just edited (dirty).
    /// </summary>
    private static Content CreateSavedContentWithOneChangedCulture()
    {
        var content = CreateVariantContent();

        content.CultureInfos!.AddOrUpdate("fr-FR", "Page fr-FR", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        content.ResetDirtyProperties(rememberDirty: false);

        content.CultureInfos!.AddOrUpdate("en-US", "Page en-US", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        content.ResetDirtyProperties(rememberDirty: true);

        return content;
    }

    [Fact]
    public void GetPublishedCultures_LiveInstance_ReturnsOnlyChangedCulture()
    {
        var content = CreatePublishedContentWithOneChangedCulture();

        // publishedCultures: null => no notification delta, so the change-tracking path is exercised
        var cultures = ContentCultureHelpers.GetPublishedCultures(content, publishedCultures: null);

        cultures.ShouldBe(new[] { "en-US" });
    }

    [Fact]
    public void GetPublishedCultures_DeepClonedInstance_FallsBackToAllPublished()
    {
        // What actually reaches the notification handler in cache / descendant-refresh paths:
        // dirty state is gone, so the helper must fall back to the full published set (#113)
        // rather than returning an empty array.
        var content = CreatePublishedContentWithOneChangedCulture();
        var clone = (IContent)content.DeepClone();

        var cultures = ContentCultureHelpers.GetPublishedCultures(clone, publishedCultures: null);

        cultures.ShouldBe(new[] { "en-US", "fr-FR" }, ignoreOrder: true);
    }

    [Fact]
    public void GetSavedCultures_LiveInstance_ReturnsOnlyChangedCulture()
    {
        var content = CreateSavedContentWithOneChangedCulture();

        var cultures = ContentCultureHelpers.GetSavedCultures(content, savedCultures: null);

        cultures.ShouldBe(new[] { "en-US" });
    }

    [Fact]
    public void GetSavedCultures_DeepClonedInstance_FallsBackToAllAvailable()
    {
        // GetSavedCultures reads CultureInfos (a different collection), but behaves the same as
        // the published helper: changed cultures on a live instance, all available on a clone.
        var content = CreateSavedContentWithOneChangedCulture();
        var clone = (IContent)content.DeepClone();

        var cultures = ContentCultureHelpers.GetSavedCultures(clone, savedCultures: null);

        cultures.ShouldBe(new[] { "en-US", "fr-FR" }, ignoreOrder: true);
    }

    [Fact]
    public void MapEvent_LiveVariantPublish_ReportsChangedCulture()
    {
        var content = CreatePublishedContentWithOneChangedCulture();

        var notification = new ContentPublishedNotification(new[] { content }, new EventMessages());

        var output = _publishedTrigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "en-US" });
    }

    [Fact]
    public void MapEvent_DeepClonedPublish_FallsBackToAllPublished()
    {
        var content = CreatePublishedContentWithOneChangedCulture();
        var clone = (IContent)content.DeepClone();

        var notification = new ContentPublishedNotification(new[] { clone }, new EventMessages());

        var output = _publishedTrigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentPublishedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "en-US", "fr-FR" }, ignoreOrder: true);
    }

    [Fact]
    public void MapEvent_DeepClonedSavedContent_FallsBackToAllAvailable()
    {
        var content = CreateSavedContentWithOneChangedCulture();
        var clone = (IContent)content.DeepClone();

        var notification = new ContentSavedNotification(new[] { clone }, new EventMessages());

        var output = _savedTrigger.MapEvent(notification)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<TriggerEvent<ContentSavedTriggerOutput>>()
            .Output;

        output.Cultures.ShouldBe(new[] { "en-US", "fr-FR" }, ignoreOrder: true);
    }
}
