using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that writes a single property value on a draft content item.
/// The change is persisted via <see cref="IContentService.Save"/> — combine with
/// <see cref="PublishContentAction"/> in a subsequent step to publish the result.
/// </summary>
[Action("umbracoAutomate.updateContentProperty", "Update Content Property",
    Description = "Writes a single property value on a content item (draft save).",
    Group = "Content",
    Icon = "icon-edit")]
public sealed class UpdateContentPropertyAction : ActionBase<UpdateContentPropertySettings, UpdateContentPropertyOutput>, ICmsAction
{
    /// <summary>
    /// Outcome emitted when the content item does not exist.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the content type.
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IContentService _contentService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly ILogger<UpdateContentPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContentPropertyAction"/> class.
    /// </summary>
    public UpdateContentPropertyAction(
        ActionInfrastructure infrastructure,
        IContentService contentService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        ILogger<UpdateContentPropertyAction> logger)
        : base(infrastructure)
    {
        _contentService = contentService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UpdateContentPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.PropertyAlias))
        {
            return ActionResult.Failed(
                new ArgumentException("Property alias is required."),
                StepRunErrorCategory.Validation);
        }

        var content = _contentService.GetById(contentKey);
        if (content is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content {ContentKey} not found.",
                context.AutomationId, context.RunId, contentKey);

            return SuccessWithOutcome(OutcomeNotFound, new UpdateContentPropertyOutput
            {
                ContentKey = contentKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        if (!content.Properties.Contains(settings.PropertyAlias))
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {ContentTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, content.ContentType.Alias);

            return SuccessWithOutcome(OutcomePropertyNotFound, new UpdateContentPropertyOutput
            {
                ContentKey = contentKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        var culture = NormaliseCulture(settings.Culture, content);
        var segment = string.IsNullOrWhiteSpace(settings.Segment) ? null : settings.Segment;

        var previousValue = content.GetValue(settings.PropertyAlias, culture, segment);

        content.SetValue(settings.PropertyAlias, settings.Value, culture, segment);

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        var result = _contentService.Save(content, userId);

        if (result.Success)
        {
            return Success(new UpdateContentPropertyOutput
            {
                ContentKey = contentKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
                Segment = segment,
                PreviousValue = previousValue,
            });
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save content '{contentKey}': {result.Result}"),
            MapErrorCategory(result.Result));
    }

    private static string? NormaliseCulture(string? requested, IContent content)
    {
        var variesByCulture = (content.ContentType.Variations & ContentVariation.Culture) != 0;
        if (!variesByCulture)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : content.AvailableCultures.FirstOrDefault();
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
