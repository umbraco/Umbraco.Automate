using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that writes CMS audit trail entries when <see cref="ICmsAction"/> actions
/// complete successfully. Audit failures are logged but never fail the action.
/// </summary>
internal sealed class AuditTrailMiddleware : IActionMiddleware
{
    private readonly IAuditEntryService _auditEntryService;
    private readonly ILogger<AuditTrailMiddleware> _logger;

    public AuditTrailMiddleware(IAuditEntryService auditEntryService, ILogger<AuditTrailMiddleware> logger)
    {
        _auditEntryService = auditEntryService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);

        if (context.Action is not ICmsAction)
        {
            return result;
        }

        if (result.Status != ActionResultStatus.Success)
        {
            return result;
        }

        if (context.ExecutionContext is null)
        {
            return result;
        }

        try
        {
            var performingDetails = context.ExecutionContext.FormatPerformingDetails();
            var eventDetails = context.ExecutionContext.FormatEventDetails(context.StepId);
            var serviceAccountKey = context.ExecutionContext.ServiceAccountKey;

            await _auditEntryService.WriteAsync(
                performingUserKey: serviceAccountKey,
                performingDetails: performingDetails,
                performingIp: "127.0.0.1",
                eventDateUtc: DateTime.UtcNow,
                affectedUserKey: null,
                affectedDetails: $"Action: {context.ActionAlias}",
                eventType: BuildEventType(context.ActionAlias),
                eventDetails: eventDetails);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write audit trail entry for action '{ActionAlias}' in run {RunId}. The action itself completed successfully.",
                context.ActionAlias,
                context.RunId);
        }

        return result;
    }

    // Umbraco's audit eventType only permits alphanumerics, hyphens, and '/' separators.
    // Action aliases use dotted segments (e.g. "umbracoAutomate.publishContent"), so promote
    // each dot to a category separator.
    private static string BuildEventType(string actionAlias)
        => $"umbraco/automate/{actionAlias.Replace('.', '/')}";
}
