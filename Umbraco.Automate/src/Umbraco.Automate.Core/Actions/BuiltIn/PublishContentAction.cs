using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that publishes a content item in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.publishContent", "Publish Content")]
public sealed class PublishContentAction : ActionBase<PublishContentActionSettings>
{
    private static readonly Guid AutomateUserKey = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly IContentPublishingService _contentPublishingService;
    private readonly ILogger<PublishContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishContentAction"/> class.
    /// </summary>
    public PublishContentAction(
        ActionInfrastructure infrastructure,
        IContentPublishingService contentPublishingService,
        ILogger<PublishContentAction> logger)
        : base(infrastructure)
    {
        _contentPublishingService = contentPublishingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override string? Description => "Publishes a content item in Umbraco CMS.";

    /// <inheritdoc />
    public override string? Group => "Content";

    /// <inheritdoc />
    public override string? Icon => "icon-globe";

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PublishContentActionSettings>();

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

        var result = await _contentPublishingService.PublishAsync(contentKey, culturesToPublish, AutomateUserKey);

        if (result.Success)
        {
            return ActionResult.Success(new
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
