using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that creates a new media item under a parent in Umbraco CMS. Media has
/// no draft / published split, so the item is live as soon as it is saved — no follow-up
/// publish step is needed.
/// </summary>
[Action("umbracoAutomate.createMedia", "Create Media",
    Description = "Creates a new media item under a parent in Umbraco CMS.",
    Group = "Media",
    Icon = "icon-add",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class CreateMediaAction : ActionBase<CreateMediaSettings, CreateMediaOutput>, ICmsAction
{
    /// <summary>
    /// Outcome emitted when the parent media item does not exist.
    /// </summary>
    public const string OutcomeParentNotFound = "parentNotFound";

    /// <summary>
    /// Outcome emitted when the media type alias doesn't resolve to a real media type.
    /// </summary>
    public const string OutcomeMediaTypeNotFound = "mediaTypeNotFound";

    /// <summary>
    /// Outcome emitted when the source file could not be downloaded. The media item is still
    /// created, without a file — a soft outcome so one unreachable image doesn't fail a run
    /// that is looping over hundreds of them.
    /// </summary>
    public const string OutcomeFileDownloadFailed = "fileDownloadFailed";

    private readonly IMediaService _mediaService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IMediaFileDownloader _fileDownloader;
    private readonly ILogger<CreateMediaAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateMediaAction"/> class.
    /// </summary>
    public CreateMediaAction(
        ActionInfrastructure infrastructure,
        IMediaService mediaService,
        IMediaTypeService mediaTypeService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        IMediaFileDownloader fileDownloader,
        ILogger<CreateMediaAction> logger)
        : base(infrastructure)
    {
        _mediaService = mediaService;
        _mediaTypeService = mediaTypeService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _fileDownloader = fileDownloader;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CreateMediaSettings>();

        if (!TryReadMediaTypeKey(settings.MediaType, out var mediaTypeKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing media type: '{settings.MediaType}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        // An empty parent means the media root. The picker can be cleared, and putting media at
        // the root is ordinary, so this is a real choice rather than a missing value.
        var atRoot = string.IsNullOrWhiteSpace(settings.ParentKey);

        Guid parentKey = Guid.Empty;
        if (!atRoot && !Guid.TryParse(settings.ParentKey, out parentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid parent key: '{settings.ParentKey}'."),
                StepRunErrorCategory.Validation);
        }

        // The root is not a node, so it takes its own check: a service account confined to a
        // start node can reach folders inside it but must not write to the root.
        var failure = atRoot
            ? await _authorizer.AuthorizeMediaRootOrFailAsync(cancellationToken)
            : await _authorizer.AuthorizeMediaOrFailAsync(parentKey, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!atRoot && _mediaService.GetById(parentKey) is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Parent media {ParentKey} not found.",
                context.AutomationId, context.RunId, parentKey);

            return SuccessWithOutcome(OutcomeParentNotFound, new CreateMediaOutput
            {
                Name = settings.Name,
                MediaTypeKey = mediaTypeKey,
                ParentKey = parentKey,
            });
        }

        var mediaType = _mediaTypeService.Get(mediaTypeKey);
        if (mediaType is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Media type {MediaTypeKey} not found.",
                context.AutomationId, context.RunId, mediaTypeKey);

            return SuccessWithOutcome(OutcomeMediaTypeNotFound, new CreateMediaOutput
            {
                Name = settings.Name,
                MediaTypeKey = mediaTypeKey,
                ParentKey = parentKey,
            });
        }

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        // The int overload takes the root sentinel; the Guid one has no way to express it.
        var media = atRoot
            ? _mediaService.CreateMedia(settings.Name, UmbracoConstants.System.Root, mediaType.Alias, userId)
            : _mediaService.CreateMedia(settings.Name, parentKey, mediaType.Alias, userId);

        ApplyProperties(media, settings.PropertiesJson);

        // Download before the save so the file and the item land in one write. A failure here
        // leaves the item fileless rather than aborting — see OutcomeFileDownloadFailed.
        var download = await DownloadFileAsync(context, media, settings.SourceUrl, cancellationToken);

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The save raises notifications (e.g. webhook delivery) that resolve
        // media URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = _mediaService.Save(media, userId);

        if (result.Success)
        {
            var output = new CreateMediaOutput
            {
                MediaKey = media.Key,
                Name = settings.Name,
                MediaTypeKey = mediaTypeKey,
                MediaTypeAlias = mediaType.Alias,
                ParentKey = parentKey,
                FileName = download?.FileName,
            };

            return download is { Success: false }
                ? SuccessWithOutcome(OutcomeFileDownloadFailed, output)
                : Success(output);
        }

        var status = result.Result?.Result ?? OperationResultType.FailedExceptionThrown;
        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save new media under '{parentKey}': {status}"),
            MapErrorCategory(status));
    }

    /// <summary>
    /// Downloads the source file onto the item's upload property, or returns <c>null</c> when no
    /// source URL was configured. A blank URL is normal — media types such as folders hold no
    /// file, and a binding that resolves to nothing (an API row with no image) should still
    /// produce the item.
    /// </summary>
    private async Task<MediaFileDownloadResult?> DownloadFileAsync(
        ActionContext context,
        IMedia media,
        string? sourceUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        var propertyAlias = _fileDownloader.DefaultFilePropertyAlias;
        if (!media.Properties.Contains(propertyAlias))
        {
            return MediaFileDownloadResult.Failed(
                $"Media type '{media.ContentType.Alias}' has no '{propertyAlias}' property to store a file on.");
        }

        var result = await _fileDownloader.DownloadToPropertyAsync(media, sourceUrl, propertyAlias, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "Automation {AutomationId} / Run {RunId}: Media file download failed. {Reason}",
                context.AutomationId, context.RunId, result.FailureReason);
        }

        return result;
    }

    /// <summary>
    /// Reads the media type key out of the picker's stored value. The picker is capped at a
    /// single selection, but it stores its value in the same comma-separated form the
    /// multi-select type pickers use, so take the first key it holds.
    /// </summary>
    private static bool TryReadMediaTypeKey(string? pickerValue, out Guid mediaTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(pickerValue))
        {
            foreach (var part in pickerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(part, out mediaTypeKey))
                {
                    return true;
                }
            }
        }

        mediaTypeKey = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Applies optional invariant property values from a JSON object. Malformed JSON and
    /// unknown property aliases are silently skipped — this is optional convenience config,
    /// not a required part of creating the media item.
    /// </summary>
    private static void ApplyProperties(IMedia media, string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return;
        }

        Dictionary<string, string>? properties;
        try
        {
            properties = JsonSerializer.Deserialize<Dictionary<string, string>>(propertiesJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (properties is null)
        {
            return;
        }

        foreach (var (alias, value) in properties)
        {
            if (media.Properties.Contains(alias))
            {
                media.SetValue(alias, value);
            }
        }
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
