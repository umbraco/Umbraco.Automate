using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that publishes a content item in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.publishContent", "Publish Content",
    Description = "Publishes a content item in Umbraco CMS.",
    Group = "Content",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionPublish.ActionLetter])]
public sealed class PublishContentAction : ActionBase<PublishContentSettings, PublishContentOutput>, ICmsAction
{
    private readonly IContentPublishingService _contentPublishingService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly ILogger<PublishContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishContentAction"/> class.
    /// </summary>
    public PublishContentAction(
        ActionInfrastructure infrastructure,
        IContentPublishingService contentPublishingService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        ILogger<PublishContentAction> logger)
        : base(infrastructure)
    {
        _contentPublishingService = contentPublishingService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PublishContentSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        var culturesToPublish = ParseCultures(settings.Cultures);

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Publishing content {ContentKey} for cultures [{Cultures}]",
            context.AutomationId, context.RunId, contentKey,
            culturesToPublish.Count == 0 ? "invariant" : string.Join(", ", culturesToPublish.Select(c => c.Culture ?? "*")));

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The publish raises notifications (e.g. webhook delivery) that resolve
        // content URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _contentPublishingService.PublishAsync(contentKey, culturesToPublish, userKey);

        if (result.Success)
        {
            return Success(new PublishContentOutput
            {
                ContentKey = contentKey,
                Cultures = culturesToPublish.Select(c => c.Culture).ToArray(),
            });
        }

        var status = result.Status;
        var errorCategory = MapErrorCategory(status);

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to publish content '{contentKey}': {status}"),
            errorCategory);
    }

    private static List<CulturePublishScheduleModel> ParseCultures(string? cultures)
    {
        if (string.IsNullOrWhiteSpace(cultures))
        {
            // Invariant content — publish with null culture, no schedule.
            return [new CulturePublishScheduleModel { Culture = null, Schedule = null }];
        }

        return cultures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(culture => new CulturePublishScheduleModel { Culture = culture, Schedule = null })
            .ToList();
    }

    private static StepRunErrorCategory MapErrorCategory(ContentPublishingOperationStatus status) => status switch
    {
        ContentPublishingOperationStatus.ContentNotFound => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.ContentInvalid => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InvalidCulture => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.MandatoryCultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.PathNotPublished => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InTrash => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.NothingToPublish => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CancelledByEvent => StepRunErrorCategory.Cancelled,
        ContentPublishingOperationStatus.ConcurrencyViolation => StepRunErrorCategory.ServiceUnavailable,
        _ => StepRunErrorCategory.Unknown,
    };
}
