using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that moves a content item to a new parent (or to the content root) in
/// Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.moveContent", "Move Content",
    Description = "Moves a content item to a new parent in Umbraco CMS.",
    Group = "Content",
    Icon = "icon-enter",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionMove.ActionLetter])]
public sealed class MoveContentAction : ActionBase<MoveContentSettings, MoveContentOutput>, ICmsAction
{
    private readonly IContentEditingService _contentEditingService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<MoveContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MoveContentAction"/> class.
    /// </summary>
    public MoveContentAction(
        ActionInfrastructure infrastructure,
        IContentEditingService contentEditingService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<MoveContentAction> logger)
        : base(infrastructure)
    {
        _contentEditingService = contentEditingService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<MoveContentSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (!TryParseTargetParentKey(settings.TargetParentKey, out var targetParentKey, out var parseFailure))
        {
            return parseFailure;
        }

        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Moving content {ContentKey} to parent {TargetParentKey}",
            context.AutomationId, context.RunId, contentKey, targetParentKey?.ToString() ?? "<root>");

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The move raises notifications (e.g. webhook delivery) that resolve
        // content URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _contentEditingService.MoveAsync(contentKey, targetParentKey, userKey);

        if (result.Success)
        {
            return Success(new MoveContentOutput
            {
                ContentKey = contentKey,
                ParentKey = targetParentKey,
            });
        }

        var status = result.Status;
        var errorCategory = MapErrorCategory(status);

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to move content '{contentKey}': {status}"),
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
