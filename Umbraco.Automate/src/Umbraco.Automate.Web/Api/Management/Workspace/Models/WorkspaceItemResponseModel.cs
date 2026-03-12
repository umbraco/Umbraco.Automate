using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Models;

/// <summary>
/// Response model for workspace list items.
/// </summary>
public sealed class WorkspaceItemResponseModel
{
    /// <summary>The workspace ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The unique alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The service account key.</summary>
    [Required]
    public Guid ServiceAccountKey { get; set; }

    /// <summary>The entity version.</summary>
    public int Version { get; set; }

    /// <summary>When the workspace was created.</summary>
    public DateTime DateCreated { get; set; }

    /// <summary>When the workspace was last modified.</summary>
    public DateTime DateModified { get; set; }
}
