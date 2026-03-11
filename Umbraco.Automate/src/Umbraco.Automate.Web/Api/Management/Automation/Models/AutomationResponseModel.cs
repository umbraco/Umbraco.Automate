using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Response model for a full automation.
/// </summary>
public sealed class AutomationResponseModel
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

    /// <summary>The lifecycle status.</summary>
    [Required]
    public AutomationStatus Status { get; set; }

    /// <summary>The published version number, or null.</summary>
    public int? PublishedVersion { get; set; }

    /// <summary>The trigger configuration.</summary>
    public TriggerConfiguration? Trigger { get; set; }

    /// <summary>The step configurations.</summary>
    public IList<StepConfiguration> Steps { get; set; } = [];

    /// <summary>The connections between steps.</summary>
    public IList<StepConnection> Connections { get; set; } = [];

    /// <summary>The serialised canvas state.</summary>
    public string? CanvasState { get; set; }

    /// <summary>The entity version.</summary>
    public int Version { get; set; }

    /// <summary>When the automation was created.</summary>
    public DateTime DateCreated { get; set; }

    /// <summary>When the automation was last modified.</summary>
    public DateTime DateModified { get; set; }
}
