using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Hosting;

namespace Umbraco.Automate.Web.Api.Management.Automation.Controllers;

/// <summary>
/// Gets the public webhook endpoint URL for an automation.
/// </summary>
[ApiVersion("1.0")]
public sealed class WebhookUrlAutomationController : AutomationControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHostingEnvironment _hostingEnvironment;
    private readonly IOptionsMonitor<WebRoutingSettings> _webRoutingSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookUrlAutomationController"/> class.
    /// </summary>
    public WebhookUrlAutomationController(
        IAutomationService automationService,
        IAuthorizationService authorizationService,
        IHostingEnvironment hostingEnvironment,
        IOptionsMonitor<WebRoutingSettings> webRoutingSettings)
    {
        _automationService = automationService;
        _authorizationService = authorizationService;
        _hostingEnvironment = hostingEnvironment;
        _webRoutingSettings = webRoutingSettings;
    }

    /// <summary>
    /// Gets the absolute URL an external caller should send webhook requests to for this
    /// automation.
    /// </summary>
    /// <remarks>
    /// Resolves the host the same way Umbraco's own <c>AspNetCoreRequestAccessor</c> does for
    /// its links back to the site (that logic isn't exposed on the public
    /// <c>IRequestAccessor</c> interface, so it's reproduced here): an explicit
    /// <c>WebRouting:UmbracoApplicationUrl</c> config value wins, so an admin behind a load
    /// balancer can pin the public host rather than have it guessed from this request.
    /// Otherwise it falls back to the current request's own scheme and host, which needs no
    /// extra configuration to work.
    /// </remarks>
    [HttpGet("{id:guid}/webhook-url")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(WebhookUrlResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWebhookUrl(Guid id, CancellationToken cancellationToken = default)
    {
        var automation = await _automationService.GetAutomationAsync(id, cancellationToken);
        if (automation is null)
        {
            return AutomationNotFound();
        }

        var forbidden = await AuthorizeWorkspaceAccessAsync(_authorizationService, automation.WorkspaceId);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var path = _hostingEnvironment.ToAbsolute($"/automate/webhook/{id}");
        var url = new Uri(ResolveApplicationUrl(), path);

        return Ok(new WebhookUrlResponseModel { Url = url.ToString() });
    }

    private Uri ResolveApplicationUrl()
    {
        var configured = _webRoutingSettings.CurrentValue.UmbracoApplicationUrl;
        return string.IsNullOrEmpty(configured)
            ? new Uri(UriHelper.BuildAbsolute(Request.Scheme, Request.Host))
            : new Uri(configured);
    }
}
