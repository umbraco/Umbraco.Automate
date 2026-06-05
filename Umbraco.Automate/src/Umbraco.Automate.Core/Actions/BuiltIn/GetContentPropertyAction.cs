using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a single property value from a published content
/// item and exposes it to downstream steps without materialising the whole node.
/// </summary>
[Action("umbracoAutomate.getContentProperty", "Get Content Property",
    Description = "Fetches a single property value from a published content item.",
    Group = "Content",
    Icon = "icon-document",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionBrowse.ActionLetter])]
public sealed class GetContentPropertyAction : ActionBase<GetContentPropertySettings, GetContentPropertyOutput>
{
    /// <summary>
    /// Outcome emitted when the item is not present in the published cache.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the content type.
    /// Separate from a null/empty value — this is a "wrong alias" signal that users
    /// may want to branch on (e.g. to surface a config error).
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<GetContentPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContentPropertyAction"/> class.
    /// </summary>
    public GetContentPropertyAction(
        ActionInfrastructure infrastructure,
        IPublishedContentCache publishedContentCache,
        IUmbracoContextFactory umbracoContextFactory,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        ILogger<GetContentPropertyAction> logger)
        : base(infrastructure)
    {
        _publishedContentCache = publishedContentCache;
        _umbracoContextFactory = umbracoContextFactory;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetContentPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) ||
            !Guid.TryParse(settings.ContentKey, out var contentKey))
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

        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var content = await _publishedContentCache.GetByIdAsync(contentKey);
        if (content is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content {ContentKey} not found in published cache.",
                context.AutomationId, context.RunId, contentKey);

            return SuccessWithOutcome(OutcomeNotFound, new GetContentPropertyOutput
            {
                ContentKey = contentKey,
                PropertyAlias = settings.PropertyAlias,
            });
        }

        var culture = NormaliseCulture(settings.Culture, content);

        if (content.GetProperty(settings.PropertyAlias) is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {ContentTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, content.ContentType.Alias);

            return SuccessWithOutcome(OutcomePropertyNotFound, new GetContentPropertyOutput
            {
                ContentKey = contentKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
            });
        }

        var value = _normaliser.ReadProperty(content, settings.PropertyAlias, culture);

        return Success(new GetContentPropertyOutput
        {
            ContentKey = contentKey,
            PropertyAlias = settings.PropertyAlias,
            Culture = culture,
            Value = value,
            HasValue = value is not null,
        });
    }

    private static string? NormaliseCulture(string? requested, IPublishedContent content)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : content.Cultures.Keys.FirstOrDefault();
    }
}
