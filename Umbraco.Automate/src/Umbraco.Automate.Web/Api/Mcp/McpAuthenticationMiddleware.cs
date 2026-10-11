using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Resolves the automation addressed by the MCP endpoint's <c>{automationId}</c> route value,
/// checks it's published with an <see cref="McpTrigger"/>, and checks the caller's bearer token
/// against the trigger's configured secret — all before the request reaches the MCP handler.
/// A blank secret is a deliberate author choice to allow unauthenticated calls.
/// </summary>
internal sealed class McpAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public McpAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAutomationService automationService, TriggerCollection triggers)
    {
        if (!context.Request.RouteValues.TryGetValue("automationId", out var raw)
            || raw is not string idText
            || !Guid.TryParse(idText, out var automationId))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var automation = await automationService.GetAutomationAsync(automationId, context.RequestAborted);
        if (automation is null || automation.Status != AutomationStatus.Published)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var trigger = automation.Trigger is not null
            ? triggers.GetByAlias<McpTrigger>(automation.Trigger.TriggerAlias)
            : null;
        if (trigger is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var settings = automation.Trigger?.Settings is { Count: > 0 } rawSettings
            ? trigger.ResolveSettings(rawSettings)
            : new McpTriggerSettings();

        if (!string.IsNullOrEmpty(settings?.Secret))
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var provided = authHeader["Bearer ".Length..];
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(settings.Secret),
                    Encoding.UTF8.GetBytes(provided)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        context.Items[McpHttpContextItems.AutomationKey] = automation;
        context.Items[McpHttpContextItems.SettingsKey] = settings;

        await _next(context);
    }
}
