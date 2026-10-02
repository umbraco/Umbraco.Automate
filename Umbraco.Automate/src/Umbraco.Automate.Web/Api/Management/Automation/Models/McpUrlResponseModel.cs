using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Response model for an automation's MCP endpoint URL.
/// </summary>
public sealed class McpUrlResponseModel
{
    /// <summary>The absolute URL an MCP client should connect to for this automation's tool.</summary>
    [Required]
    public string Url { get; set; } = string.Empty;
}
