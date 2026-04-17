using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered webhook authentication strategies.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllWebhookAuthenticatorsController : CatalogueControllerBase
{
    private readonly WebhookAuthenticatorCollection _authenticators;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllWebhookAuthenticatorsController"/> class.
    /// </summary>
    public AllWebhookAuthenticatorsController(WebhookAuthenticatorCollection authenticators, IUmbracoMapper mapper)
    {
        _authenticators = authenticators;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered webhook authenticators with their alias, display name, and description.
    /// </summary>
    [HttpGet("webhook-authenticators")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<WebhookAuthenticatorItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<WebhookAuthenticatorItemResponseModel>> GetAllWebhookAuthenticators()
    {
        return Ok(_mapper.MapEnumerable<IWebhookAuthenticator, WebhookAuthenticatorItemResponseModel>(_authenticators));
    }
}
