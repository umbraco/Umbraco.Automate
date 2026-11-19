using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Resolves the automation addressed by the MCP endpoint's <c>{automationId}</c> route value,
/// loads its published version, checks it has an <see cref="McpTrigger"/>, and checks the caller's
/// bearer token against that version's secret — all before the request reaches the MCP handler.
/// A secret is required: an automation without one is refused, never served unauthenticated.
/// </summary>
/// <remarks>
/// <para>
/// Everything here reads the published version, never the current draft: a saved but unpublished
/// change (a cleared secret, an added step) must not go live until it is published, and the run is
/// recorded against the published version.
/// </para>
/// <para>
/// Every way a request can fail before it is authenticated answers <c>401</c>, as the webhook
/// endpoint does, so a caller can't tell a missing, unpublished or misconfigured automation from a
/// wrong secret. The reason is logged instead.
/// </para>
/// </remarks>
internal sealed class McpAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpAuthenticationMiddleware> _logger;

    public McpAuthenticationMiddleware(RequestDelegate next, ILogger<McpAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAutomationService automationService,
        IEntityVersionService versionService,
        TriggerCollection triggers)
    {
        if (!context.Request.RouteValues.TryGetValue("automationId", out var raw)
            || raw is not string idText
            || !Guid.TryParse(idText, out var automationId))
        {
            Unauthorized(context);
            return;
        }

        var automation = await automationService.GetAutomationAsync(automationId, context.RequestAborted);
        if (automation is null
            || automation.Status != AutomationStatus.Published
            || automation.PublishedVersion is not { } publishedVersion)
        {
            Unauthorized(context);
            return;
        }

        var published = await versionService.GetVersionSnapshotAsync<Automation>(
            automationId, publishedVersion, context.RequestAborted);
        if (published is null)
        {
            // Unlike trigger dispatch, don't fall back to the current state: it may hold unpublished
            // changes, including to the secret.
            _logger.LogWarning(
                "MCP call refused: published version {Version} of automation {AutomationId} was not found.",
                publishedVersion, automationId);
            Unauthorized(context);
            return;
        }

        var trigger = published.Trigger is not null
            ? triggers.GetByAlias<McpTrigger>(published.Trigger.TriggerAlias)
            : null;
        if (trigger is null)
        {
            Unauthorized(context);
            return;
        }

        McpTriggerSettings? settings;
        try
        {
            settings = trigger.ResolveSettings(published.Trigger!.Settings);
        }
        catch (SettingsResolutionException ex)
        {
            _logger.LogWarning(
                ex,
                "MCP call refused: the MCP trigger settings of automation {AutomationId} are invalid. Fix them and republish.",
                automationId);
            Unauthorized(context);
            return;
        }

        if (settings is null || string.IsNullOrEmpty(settings.ToolName))
        {
            _logger.LogWarning(
                "MCP call refused: automation {AutomationId} has no MCP tool name. Set one and republish.",
                automationId);
            Unauthorized(context);
            return;
        }

        if (string.IsNullOrEmpty(settings.Secret))
        {
            _logger.LogWarning(
                "MCP call refused: automation {AutomationId} has no MCP secret. Set one and republish.",
                automationId);
            Unauthorized(context);
            return;
        }

        if (!HasValidBearerToken(context.Request, settings.Secret))
        {
            Unauthorized(context);
            return;
        }

        context.Items[McpHttpContextItems.AutomationKey] = published;
        context.Items[McpHttpContextItems.SettingsKey] = settings;

        await _next(context);
    }

    private static bool HasValidBearerToken(HttpRequest request, string secret)
    {
        var authHeader = request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var provided = authHeader["Bearer ".Length..];
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(provided));
    }

    private static void Unauthorized(HttpContext context)
        => context.Response.StatusCode = StatusCodes.Status401Unauthorized;
}
