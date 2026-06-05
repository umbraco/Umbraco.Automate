using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that writes a single property value on a media item. The change is
/// persisted via <see cref="IMediaService.Save(IMedia, int)"/> — media has no draft /
/// published split so this is the only step needed to commit the change.
/// </summary>
[Action("umbracoAutomate.updateMediaProperty", "Update Media Property",
    Description = "Writes a single property value on a media item.",
    Group = "Media",
    Icon = "icon-edit",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class UpdateMediaPropertyAction : ActionBase<UpdateMediaPropertySettings, UpdateMediaPropertyOutput>, ICmsAction
{
    /// <summary>
    /// Outcome emitted when the media item does not exist.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the media type.
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IMediaService _mediaService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<UpdateMediaPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMediaPropertyAction"/> class.
    /// </summary>
    public UpdateMediaPropertyAction(
        ActionInfrastructure infrastructure,
        IMediaService mediaService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<UpdateMediaPropertyAction> logger)
        : base(infrastructure)
    {
        _mediaService = mediaService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UpdateMediaPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.MediaKey) || !Guid.TryParse(settings.MediaKey, out var mediaKey))
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

        var media = _mediaService.GetById(mediaKey);
        if (media is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Media {MediaKey} not found.",
                context.AutomationId, context.RunId, mediaKey);

            return SuccessWithOutcome(OutcomeNotFound, new UpdateMediaPropertyOutput
            {
                MediaKey = mediaKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        if (!media.Properties.Contains(settings.PropertyAlias))
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {MediaTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, media.ContentType.Alias);

            return SuccessWithOutcome(OutcomePropertyNotFound, new UpdateMediaPropertyOutput
            {
                MediaKey = mediaKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        var culture = NormaliseCulture(settings.Culture, media);
        var segment = string.IsNullOrWhiteSpace(settings.Segment) ? null : settings.Segment;

        var previousValue = media.GetValue(settings.PropertyAlias, culture, segment);

        media.SetValue(settings.PropertyAlias, settings.Value, culture, segment);

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The save raises notifications (e.g. webhook delivery) that resolve
        // media URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = _mediaService.Save(media, userId);

        if (result.Success)
        {
            return Success(new UpdateMediaPropertyOutput
            {
                MediaKey = mediaKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
                Segment = segment,
                PreviousValue = previousValue,
            });
        }

        var status = result.Result?.Result ?? OperationResultType.FailedExceptionThrown;
        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save media '{mediaKey}': {status}"),
            MapErrorCategory(status));
    }

    private static string? NormaliseCulture(string? requested, IMedia media)
    {
        var variesByCulture = (media.ContentType.Variations & ContentVariation.Culture) != 0;
        if (!variesByCulture)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : media.AvailableCultures.FirstOrDefault();
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
