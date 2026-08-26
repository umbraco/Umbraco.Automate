using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Response model for an automation's webhook endpoint URL.
/// </summary>
public sealed class WebhookUrlResponseModel
{
    /// <summary>The absolute URL an external caller should send webhook requests to.</summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}
