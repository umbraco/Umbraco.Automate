using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    /// See <see cref="ApplicationUrlResolver"/> for how the host is resolved.
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
        var url = ApplicationUrlResolver.Resolve(Request, _webRoutingSettings, path);

        return Ok(new WebhookUrlResponseModel { Url = url.ToString() });
    }
}
