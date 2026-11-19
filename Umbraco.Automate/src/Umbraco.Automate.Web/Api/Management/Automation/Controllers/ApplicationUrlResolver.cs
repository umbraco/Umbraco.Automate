using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Resolves absolute URLs for automation endpoints (webhook, MCP) the same way Umbraco's own
/// <c>AspNetCoreRequestAccessor</c> does for its links back to the site (that logic isn't
/// exposed on the public <c>IRequestAccessor</c> interface, so it's reproduced here): an
/// explicit <c>WebRouting:UmbracoApplicationUrl</c> config value wins, so an admin behind a
/// load balancer can pin the public host rather than have it guessed from the request.
/// Otherwise it falls back to the current request's own scheme and host, which needs no extra
/// configuration to work.
/// </summary>
internal static class ApplicationUrlResolver
{
    /// <summary>
    /// Resolves the absolute URL for <paramref name="absolutePath"/> against the configured
    /// application URL, or the current request's scheme and host if none is configured.
    /// </summary>
    public static Uri Resolve(HttpRequest request, IOptionsMonitor<WebRoutingSettings> webRoutingSettings, string absolutePath)
    {
        var configured = webRoutingSettings.CurrentValue.UmbracoApplicationUrl;
        var baseUrl = string.IsNullOrEmpty(configured)
            ? new Uri(UriHelper.BuildAbsolute(request.Scheme, request.Host))
            : new Uri(configured);

        return new Uri(baseUrl, absolutePath);
    }
}
