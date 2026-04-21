using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that pushes a realtime toast notification to any backoffice user currently
/// editing the specified content item.
/// </summary>
[Action("umbracoAutomate.notifyEditor", "Notify Editor",
    Description = "Sends a realtime toast to any backoffice user currently editing the specified content item.",
    Group = "Content",
    Icon = "icon-megaphone")]
public sealed class NotifyEditorAction : ActionBase<NotifyEditorSettings, NotifyEditorOutput>
{
    /// <summary>
    /// Outcome emitted when no content item exists for the configured key.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    private readonly IContentService _contentService;
    private readonly IAutomationService _automationService;
    private readonly IEditorNotifier _editorNotifier;
    private readonly ILogger<NotifyEditorAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotifyEditorAction"/> class.
    /// </summary>
    public NotifyEditorAction(
        ActionInfrastructure infrastructure,
        IContentService contentService,
        IAutomationService automationService,
        IEditorNotifier editorNotifier,
        ILogger<NotifyEditorAction> logger)
        : base(infrastructure)
    {
        _contentService = contentService;
        _automationService = automationService;
        _editorNotifier = editorNotifier;
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

            return SuccessWithOutcome(OutcomeNotFound, new NotifyEditorOutput { ContentKey = contentKey });
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

        return Success(new NotifyEditorOutput
        {
            ContentKey = contentKey,
            ContentName = content.Name ?? string.Empty,
            Title = title,
        });
    }
}
