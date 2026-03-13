using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that writes CMS audit trail entries when <see cref="ICmsAction"/> actions
/// complete successfully. Audit failures are logged but never fail the action.
/// </summary>
internal sealed class AuditTrailMiddleware : IActionMiddleware
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditTrailMiddleware> _logger;

    public AuditTrailMiddleware(IAuditService auditService, ILogger<AuditTrailMiddleware> logger)
    {
        _auditService = auditService;
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

            _auditService.Write(
                performingUserId: -1,
                performingDetails: performingDetails,
                performingIp: "127.0.0.1",
                eventDateUtc: DateTime.UtcNow,
                affectedUserId: -1,
                affectedDetails: $"Action: {context.ActionAlias}",
                eventType: $"umbraco/automate/{context.ActionAlias}",
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
}
