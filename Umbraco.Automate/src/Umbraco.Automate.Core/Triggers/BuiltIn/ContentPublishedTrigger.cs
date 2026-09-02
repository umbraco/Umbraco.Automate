using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when content is published in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per published content item.
/// </summary>
[Trigger("umbracoAutomate.contentPublished", "Content Published",
    Description = "Fires when content is published.",
    Group = "Content",
    Icon = "icon-document",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class ContentPublishedTrigger
    : NotificationTriggerBase<ContentPublishedTriggerSettings, ContentPublishedTriggerOutput, ContentPublishedNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<ContentPublishedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPublishedTrigger"/> class.
    /// </summary>
    public ContentPublishedTrigger(
        TriggerInfrastructure infrastructure,
        IUserService userService,
        ILogger<ContentPublishedTrigger> logger)
        : base(infrastructure)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(ContentPublishedNotification notification)
    {
        // Bulk/branch publishes share one publisher across many entities — classify each
        // distinct publisher id once per notification.
        var kindByPublisher = new Dictionary<int, string?>();

        foreach (var content in notification.PublishedEntities)
        {
            string? publisherKind = null;
            if (content.PublisherId is { } publisherId && !kindByPublisher.TryGetValue(publisherId, out publisherKind))
            {
                publisherKind = ContentPublisherResolver.Resolve(_userService, publisherId, _logger);
                kindByPublisher[publisherId] = publisherKind;
            }

            yield return new TriggerEvent<ContentPublishedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                // Key on the version that just got published: rapid successive publishes
                // get distinct PublishedVersionIds and therefore distinct keys, while a
                // duplicate notification for the same publish collapses to one message.
                IdempotencyKey = GenerateIdempotencyKey(content.Key, content.PublishedVersionId),
                Output = new ContentPublishedTriggerOutput
                {
                    ContentKey = content.Key,
                    ContentName = content.Name,
                    ContentTypeKey = content.ContentType?.Key,
                    ContentTypeAlias = content.ContentType?.Alias,
                    Cultures = ContentCultureHelpers.GetPublishedCultures(content, notification.PublishedCultures),
                    PublisherId = content.PublisherId,
                    PublisherKind = publisherKind,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(ContentPublishedTriggerOutput output, ContentPublishedTriggerSettings? settings)
        => EntityTypesFilter.Matches(output.ContentTypeKey, settings?.ContentTypes)
           && PublisherKindFilter.Matches(output.PublisherKind, settings?.PublishedBy);
}
