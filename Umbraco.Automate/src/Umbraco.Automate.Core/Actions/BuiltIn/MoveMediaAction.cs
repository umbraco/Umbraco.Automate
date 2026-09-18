using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that moves a media item to a new parent (or to the media root) in
/// Umbraco CMS. Media has no per-permission verbs in Umbraco, so access is gated on the
/// Media section (and start node) only — see <see cref="IAutomationActionAuthorizer.AuthorizeMediaAsync"/>.
/// </summary>
[Action("umbracoAutomate.moveMedia", "Move Media",
    Description = "Moves a media item to a new parent in Umbraco CMS.",
    Group = "Media",
    Icon = "icon-enter",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class MoveMediaAction : ActionBase<MoveMediaSettings, MoveMediaOutput>, ICmsAction
{
    private readonly IMediaEditingService _mediaEditingService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<MoveMediaAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveMediaAction"/> class.
    /// </summary>
    public MoveMediaAction(
        ActionInfrastructure infrastructure,
        IMediaEditingService mediaEditingService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<MoveMediaAction> logger)
        : base(infrastructure)
    {
        _mediaEditingService = mediaEditingService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<MoveMediaSettings>();

        if (string.IsNullOrWhiteSpace(settings.MediaKey) || !Guid.TryParse(settings.MediaKey, out var mediaKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing media key: '{settings.MediaKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (!TryParseTargetParentKey(settings.TargetParentKey, out var targetParentKey, out var parseFailure))
        {
            return parseFailure;
        }

        if (await _authorizer.AuthorizeMediaOrFailAsync(mediaKey, cancellationToken) is { } failure)
        {
            return failure;
        }

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Moving media {MediaKey} to parent {TargetParentKey}",
            context.AutomationId, context.RunId, mediaKey, targetParentKey?.ToString() ?? "<root>");

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The move raises notifications (e.g. webhook delivery) that resolve
        // media URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _mediaEditingService.MoveAsync(mediaKey, targetParentKey, userKey);

        if (result.Success)
        {
            return Success(new MoveMediaOutput
            {
                MediaKey = mediaKey,
                ParentKey = targetParentKey,
            });
        }

        var status = result.Status;
        var errorCategory = MapErrorCategory(status);

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to move media '{mediaKey}': {status}"),
            errorCategory);
    }

    private static bool TryParseTargetParentKey(string? value, out Guid? targetParentKey, out ActionResult failure)
    {
        targetParentKey = null;
        failure = null!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            failure = ActionResult.Failed(
                new ArgumentException($"Invalid target parent key: '{value}'."),
                StepRunErrorCategory.Validation);
            return false;
        }

        targetParentKey = parsed;
        return true;
    }

    private static StepRunErrorCategory MapErrorCategory(ContentEditingOperationStatus status) => status switch
    {
        ContentEditingOperationStatus.NotFound => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.InvalidKey => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.ParentNotFound => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.ParentInvalid => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.NotAllowed => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.InTrash => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.CancelledByNotification => StepRunErrorCategory.Cancelled,
        _ => StepRunErrorCategory.Unknown,
    };
}
