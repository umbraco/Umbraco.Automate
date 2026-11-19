using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// The pieces that put the MCP endpoint together. Production registers them through Umbraco's
/// pipeline filters, and the endpoint wiring test calls the same methods on a plain ASP.NET Core
/// pipeline, so a change here can't leave the test checking a copy.
/// </summary>
internal static class McpEndpointWiring
{
    /// <summary>
    /// Used to scope the MCP middleware to MCP requests only. Backed by
    /// <see cref="Constants.McpApi.PathPrefix"/> — its own literal source of truth, not derived
    /// from <see cref="Constants.McpApi.RouteTemplate"/> — so a future change to the route
    /// template's shape can't silently break or widen this scoping.
    /// </summary>
    private static readonly PathString PathPrefix = Constants.McpApi.PathPrefix;

    /// <summary>
    /// Configures the MCP server's HTTP transport: stateless, with the one tool for the automation
    /// <see cref="McpAuthenticationMiddleware"/> resolved for this request.
    /// </summary>
    /// <param name="options">The transport options to configure.</param>
    /// <param name="initialPollInterval">
    /// How often the tool first checks whether its run has finished. <c>null</c> uses the default;
    /// tests pass a shorter one.
    /// </param>
    public static void ConfigureHttpTransport(HttpServerTransportOptions options, TimeSpan? initialPollInterval = null)
    {
        // Stateless is required so ConfigureSessionOptions below runs on every request
        // rather than once per long-lived session, which is what makes per-automation tool
        // resolution correct without needing session affinity across Automate's own app
        // instances. It already defaults to true in ModelContextProtocol 2.2.0, so this is
        // defensive rather than load-bearing.
        options.Stateless = true;

        options.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
        {
            if (httpContext.Items[McpHttpContextItems.AutomationKey] is Automation automation
                && httpContext.Items[McpHttpContextItems.SettingsKey] is McpTriggerSettings settings)
            {
                var services = httpContext.RequestServices;
                mcpOptions.ToolCollection =
                [
                    new AutomationMcpTool(
                        automation,
                        settings,
                        services.GetRequiredService<IAutomationExecutor>(),
                        services.GetRequiredService<IAutomationRunService>(),
                        services.GetRequiredService<IRunDataSanitizer>(),
                        initialPollInterval),
                ];
            }

            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Adds the MCP rate limit policy: <see cref="McpOptions.RateLimitPerMinute"/> calls per
    /// automation per minute.
    /// </summary>
    public static void AddRateLimitPolicy(RateLimiterOptions options)
        => options.AddPolicy(Constants.McpApi.RateLimitPolicy, context =>
        {
            var mcpOptions = context.RequestServices.GetRequiredService<IOptions<McpOptions>>().Value;
            var partitionKey = context.Request.RouteValues["automationId"]?.ToString() ?? "global";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = mcpOptions.RateLimitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                });
        });

    /// <summary>
    /// Before routing: caps the MCP request body size.
    /// </summary>
    public static void UsePreRouting(IApplicationBuilder app)
        => app.UseWhen(
            context => context.Request.Path.StartsWithSegments(PathPrefix),
            branch => branch.UseMiddleware<McpRequestBodyLimitMiddleware>());

    /// <summary>
    /// After routing, so <c>{automationId}</c> is in the route values: authenticates the request.
    /// </summary>
    /// <remarks>
    /// Post-routing middleware runs for every request in the app, not just this endpoint, so
    /// <see cref="McpAuthenticationMiddleware"/> — which refuses any request without an
    /// <c>automationId</c> route value — must be scoped to the MCP path. Unscoped, every other
    /// route in the site (the backoffice, content, other APIs) would be refused too.
    /// </remarks>
    public static void UsePostRouting(IApplicationBuilder app)
        => app.UseWhen(
            context => context.Request.Path.StartsWithSegments(PathPrefix),
            branch => branch.UseMiddleware<McpAuthenticationMiddleware>());

    /// <summary>
    /// Maps the MCP endpoint, with its rate limit.
    /// </summary>
    public static void MapEndpoints(IEndpointRouteBuilder endpoints)
        => endpoints.MapMcp(Constants.McpApi.RouteTemplate)
            .RequireRateLimiting(Constants.McpApi.RateLimitPolicy);
}
