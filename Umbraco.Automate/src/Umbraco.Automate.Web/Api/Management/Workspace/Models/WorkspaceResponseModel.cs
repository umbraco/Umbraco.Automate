using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Workspace.Models;

/// <summary>
/// Response model for a full workspace.
/// </summary>
public sealed class WorkspaceResponseModel
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

    /// <summary>User group keys with access.</summary>
    public IList<Guid> UserGroups { get; set; } = [];

    /// <summary>Connection IDs available in this workspace.</summary>
    public IList<Guid> Connections { get; set; } = [];

    /// <summary>The entity version.</summary>
    public int Version { get; set; }

    /// <summary>When the workspace was created.</summary>
    public DateTime DateCreated { get; set; }

    /// <summary>When the workspace was last modified.</summary>
    public DateTime DateModified { get; set; }
}
