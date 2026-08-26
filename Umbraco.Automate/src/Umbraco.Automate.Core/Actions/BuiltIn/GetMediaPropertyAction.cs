using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a single property value from a media item and exposes
/// it to downstream steps without materialising the whole item.
/// </summary>
[Action("umbracoAutomate.getMediaProperty", "Get Media Property",
    Description = "Fetches a single property value from a media item.",
    Group = "Media",
    Icon = "icon-picture",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class GetMediaPropertyAction : ActionBase<GetMediaPropertySettings, GetMediaPropertyOutput>
{
    /// <summary>
    /// Outcome emitted when the item is not present in the published media cache.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the media type.
    /// Separate from a null/empty value — this is a "wrong alias" signal that users
    /// may want to branch on (e.g. to surface a config error).
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IPublishedMediaCache _publishedMediaCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ILogger<GetMediaPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMediaPropertyAction"/> class.
    /// </summary>
    public GetMediaPropertyAction(
        ActionInfrastructure infrastructure,
        IPublishedMediaCache publishedMediaCache,
        IUmbracoContextFactory umbracoContextFactory,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        IVariationContextAccessor variationContextAccessor,
        ILogger<GetMediaPropertyAction> logger)
        : base(infrastructure)
    {
        _publishedMediaCache = publishedMediaCache;
        _umbracoContextFactory = umbracoContextFactory;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _variationContextAccessor = variationContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetMediaPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.MediaKey) ||
            !Guid.TryParse(settings.MediaKey, out var mediaKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing media key: '{settings.MediaKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.PropertyAlias))
        {
            return ActionResult.Failed(
                new ArgumentException("Property alias is required."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeMediaOrFailAsync(mediaKey, cancellationToken) is { } failure)
        {
            return failure;
        }

        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var media = await _publishedMediaCache.GetByIdAsync(mediaKey);
        if (media is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Media {MediaKey} not found in published cache.",
                context.AutomationId, context.RunId, mediaKey);

            return SuccessWithOutcome(OutcomeNotFound, new GetMediaPropertyOutput
            {
                MediaKey = mediaKey,
                PropertyAlias = settings.PropertyAlias,
            });
        }

        var culture = NormaliseCulture(settings.Culture, media);

        if (media.GetProperty(settings.PropertyAlias) is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {MediaTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, media.ContentType.Alias);

            return SuccessWithOutcome(OutcomePropertyNotFound, new GetMediaPropertyOutput
            {
                MediaKey = mediaKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
            });
        }

        // Property reads need an ambient VariationContext to resolve the culture and segment
        // they were not given. There is none on the automation thread, and a null segment is
        // rejected by the published cache. See EnterVariationContext.
        using var variationScope = _variationContextAccessor.EnterVariationContext(culture);

        var value = _normaliser.ReadProperty(media, settings.PropertyAlias, culture);

        return Success(new GetMediaPropertyOutput
        {
            MediaKey = mediaKey,
            PropertyAlias = settings.PropertyAlias,
            Culture = culture,
            Value = value,
            HasValue = value is not null,
        });
    }

    private static string? NormaliseCulture(string? requested, IPublishedContent media)
    {
        if (!media.ContentType.VariesByCulture())
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : media.Cultures.Keys.FirstOrDefault();
    }
}
