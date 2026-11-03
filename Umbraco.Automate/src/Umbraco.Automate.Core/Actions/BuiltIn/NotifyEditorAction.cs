using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that pushes a realtime toast notification to any backoffice user currently
/// editing the specified content item.
/// </summary>
[Action("umbracoAutomate.notifyEditor", "Notify Editor",
    Description = "Sends a realtime toast to any backoffice user currently editing the specified content item.",
    Group = "Content",
    Icon = "icon-megaphone",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionBrowse.ActionLetter])]
public sealed class NotifyEditorAction : ActionBase<NotifyEditorSettings, NotifyEditorOutput>
{
    /// <summary>
    /// Outcome emitted when no content item exists for the configured key.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    private readonly IContentService _contentService;
    private readonly IAutomationService _automationService;
    private readonly IEditorNotifier _editorNotifier;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<NotifyEditorAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyEditorAction"/> class.
    /// </summary>
    [Obsolete("Use the constructor that takes an IAutomationActionAuthorizer. Scheduled for removal in Umbraco Automate 19.")]
    public NotifyEditorAction(
        ActionInfrastructure infrastructure,
        IContentService contentService,
        IAutomationService automationService,
        IEditorNotifier editorNotifier,
        ILogger<NotifyEditorAction> logger)
        : this(
            infrastructure,
            contentService,
            automationService,
            editorNotifier,
            StaticServiceProvider.Instance.GetRequiredService<IAutomationActionAuthorizer>(),
            logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyEditorAction"/> class.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public NotifyEditorAction(
        ActionInfrastructure infrastructure,
        IContentService contentService,
        IAutomationService automationService,
        IEditorNotifier editorNotifier,
        IAutomationActionAuthorizer authorizer,
        ILogger<NotifyEditorAction> logger)
        : base(infrastructure)
    {
        _contentService = contentService;
        _automationService = automationService;
        _editorNotifier = editorNotifier;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<NotifyEditorSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        // Use IContentService so editors on unpublished / draft content still get notified.
        var content = _contentService.GetById(contentKey);
        if (content is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content {ContentKey} not found — skipping editor notification.",
                context.AutomationId, context.RunId, contentKey);

            context.LogWarning($"Content {contentKey} was not found, so no editors were notified");

            return SuccessWithOutcome(OutcomeNotFound, new NotifyEditorOutput { ContentKey = contentKey });
        }

        // Node-level authorisation: the service account's start node / granular permissions may
        // scope it to a subset of the Content section. This runs after the existence check on
        // purpose: the real authorizer reports a missing node as a failure, which would turn the
        // documented notFound outcome into an Authentication error. Nothing about the item (its
        // name) is read or logged before this point.
        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        // Resolve the automation name up-front — it's used in both default title and
        // default body so the editor knows which automation just affected them.
        var automation = await _automationService.GetAutomationAsync(context.AutomationId, cancellationToken);
        var automationName = string.IsNullOrWhiteSpace(automation?.Name) ? "An automation" : automation!.Name;

        var title = string.IsNullOrWhiteSpace(settings.Title)
            ? "Automation update"
            : settings.Title!;

        var body = string.IsNullOrWhiteSpace(settings.Message)
            ? $"\"{automationName}\" just ran on the content you're editing."
            : settings.Message!;

        var severity = Enum.TryParse<EditorNotificationSeverity>(settings.Severity, ignoreCase: true, out var parsed)
            ? parsed
            : EditorNotificationSeverity.Default;

        var message = new EditorNotificationMessage
        {
            ContentKey = contentKey,
            Title = title,
            Message = body,
            Severity = severity,
        };

        await _editorNotifier.NotifyAsync(message, cancellationToken);

        context.LogInfo($"Sent a notification to anyone editing {ActionLogFormat.Item(content.Name, contentKey)}");

        return Success(new NotifyEditorOutput
        {
            ContentKey = contentKey
        });
    }
}
