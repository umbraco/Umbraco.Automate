using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that unpublishes a Library element in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.unpublishElement", "Unpublish Element",
    Description = "Unpublishes a Library element in Umbraco CMS.",
    Group = "Library",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementUnpublish.ActionLetter])]
public sealed class UnpublishElementAction : ActionBase<UnpublishElementSettings, UnpublishElementOutput>, ICmsAction
{
    private readonly IElementPublishingService _elementPublishingService;
    private readonly IElementService _elementService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<UnpublishElementAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpublishElementAction"/> class.
    /// </summary>
    public UnpublishElementAction(
        ActionInfrastructure infrastructure,
        IElementPublishingService elementPublishingService,
        IElementService elementService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<UnpublishElementAction> logger)
        : base(infrastructure)
    {
        _elementPublishingService = elementPublishingService;
        _elementService = elementService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UnpublishElementSettings>();

        if (string.IsNullOrWhiteSpace(settings.ElementKey) || !Guid.TryParse(settings.ElementKey, out var elementKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing element key: '{settings.ElementKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeElementOrFailAsync(elementKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        // A service account with a root start node is authorised for the recycle bin too, so a
        // trashed element gets this far. Fail it, as a missing one does.
        var element = _elementService.GetById(elementKey);
        if (element is null || element.Trashed)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Library element '{elementKey}' was not found or is in the recycle bin."),
                StepRunErrorCategory.Validation);
        }

        var requested = ParseCultures(settings.Cultures);

        // The CMS refuses null for a culture-variant element and any culture for an invariant one,
        // so "all cultures" is "*" or null depending on the element's type.
        var cultures = requested
            ?? (element.ContentType.VariesByCulture() ? new HashSet<string> { "*" } : null);

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Unpublishing Library element {ElementKey} for cultures [{Cultures}]",
            context.AutomationId, context.RunId, elementKey,
            cultures is null ? "invariant" : string.Join(", ", cultures));

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _elementPublishingService.UnpublishAsync(elementKey, cultures, userKey);

        if (result.Success)
        {
            var item = ActionLogFormat.Item(element.Name, elementKey);
            context.LogInfo(requested is null
                ? $"Unpublished {item}"
                : $"Unpublished {item} in {ActionLogFormat.Cultures(requested)}");

            return Success(new UnpublishElementOutput
            {
                ElementKey = elementKey,
                Cultures = requested?.ToArray(),
            });
        }

        var status = result.Result;

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to unpublish Library element '{elementKey}': {status}"),
            MapErrorCategory(status));
    }

    private static HashSet<string>? ParseCultures(string? cultures)
    {
        if (string.IsNullOrWhiteSpace(cultures))
        {
            return null;
        }

        return cultures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static StepRunErrorCategory MapErrorCategory(ContentPublishingOperationStatus status) => status switch
    {
        ContentPublishingOperationStatus.ContentNotFound => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.ContentInvalid => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InvalidCulture => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.MandatoryCultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CannotPublishInvariantWhenVariant => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CannotPublishVariantWhenNotVariant => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InTrash => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CancelledByEvent => StepRunErrorCategory.Cancelled,
        ContentPublishingOperationStatus.ConcurrencyViolation => StepRunErrorCategory.ServiceUnavailable,
        _ => StepRunErrorCategory.Unknown,
    };
}
