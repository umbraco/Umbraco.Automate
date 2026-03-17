using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Response model for automation list items (no definition details).
/// </summary>
public sealed class AutomationItemResponseModel
{
    /// <summary>The automation ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The unique alias.</summary>
    [Required]
    public string Alias { get; set; } = string.Empty;

    /// <summary>The display name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether triggers are active.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>The workspace this automation belongs to.</summary>
    [Required]
    public Guid WorkspaceId { get; set; }

    /// <summary>The group (folder) this automation belongs to, or null.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>The lifecycle status.</summary>
    [Required]
    public AutomationStatus Status { get; set; }

    /// <summary>The entity version.</summary>
    public int Version { get; set; }

    /// <summary>When the automation was created.</summary>
    public DateTime DateCreated { get; set; }

    /// <summary>When the automation was last modified.</summary>
    public DateTime DateModified { get; set; }
}
