using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Connection.Models;

/// <summary>
/// Response model for connection list items.
/// </summary>
public sealed class ConnectionItemResponseModel
{
    /// <summary>The connection ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The unique alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The connection type alias (e.g. "slack", "smtp").</summary>
    [Required]
    public string Type { get; set; } = string.Empty;

    /// <summary>The entity version.</summary>
    public int Version { get; set; }

    /// <summary>When the connection was created.</summary>
    public DateTime DateCreated { get; set; }

    /// <summary>When the connection was last modified.</summary>
    public DateTime DateModified { get; set; }
}
