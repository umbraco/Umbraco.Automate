using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that unpublishes a content item in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.unpublishContent", "Unpublish Content",
    Description = "Unpublishes a content item in Umbraco CMS.",
    Group = "Content",
    Icon = "icon-globe")]
public sealed class UnpublishContentAction : ActionBase<UnpublishContentActionSettings>, ICmsAction
{
    private readonly IContentPublishingService _contentPublishingService;
    private readonly ILogger<UnpublishContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnpublishContentAction"/> class.
    /// </summary>
    public UnpublishContentAction(
        ActionInfrastructure infrastructure,
        IContentPublishingService contentPublishingService,
        ILogger<UnpublishContentAction> logger)
        : base(infrastructure)
    {
        _contentPublishingService = contentPublishingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UnpublishContentActionSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        var cultures = ParseCultures(settings.Cultures);

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Unpublishing content {ContentKey} for cultures [{Cultures}]",
            context.AutomationId, context.RunId, contentKey,
            cultures is null ? "invariant" : string.Join(", ", cultures));

        var userKey = context.ExecutionContext?.ServiceAccountKey
            ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

        var result = await _contentPublishingService.UnpublishAsync(contentKey, cultures, userKey);

        if (result.Success)
        {
            return ActionResult.Success(new
            {
                ContentKey = contentKey,
                Cultures = cultures?.ToArray(),
            });
        }

        var status = result.Result;
        var errorCategory = MapErrorCategory(status);

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to unpublish content '{contentKey}': {status}"),
            errorCategory);
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
        ContentPublishingOperationStatus.InTrash => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CancelledByEvent => StepRunErrorCategory.Cancelled,
        ContentPublishingOperationStatus.ConcurrencyViolation => StepRunErrorCategory.ServiceUnavailable,
        _ => StepRunErrorCategory.Unknown,
    };
}
