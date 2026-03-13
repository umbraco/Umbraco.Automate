using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered notification channel types.
/// </summary>
[ApiVersion("1.0")]
public sealed class AllNotificationChannelsController : CatalogueControllerBase
{
    private readonly NotificationChannelCollection _channels;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllNotificationChannelsController"/> class.
    /// </summary>
    public AllNotificationChannelsController(NotificationChannelCollection channels, IUmbracoMapper mapper)
    {
        _channels = channels;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered notification channel types with their metadata and settings schemas.
    /// </summary>
    [HttpGet("notification-channels")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<NotificationChannelItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<NotificationChannelItemResponseModel>> GetAllNotificationChannels()
    {
        return Ok(_mapper.MapEnumerable<INotificationChannel, NotificationChannelItemResponseModel>(_channels));
    }
}
