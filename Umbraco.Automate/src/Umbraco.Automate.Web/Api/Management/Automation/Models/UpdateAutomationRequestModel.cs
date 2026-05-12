using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Request model for updating an existing automation.
/// </summary>
public sealed class UpdateAutomationRequestModel
{
    /// <summary>The unique alias.</summary>
    [Required]
    public required string Alias { get; init; }

    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>Optional description.</summary>
    public string? Description { get; init; }

    /// <summary>The group (folder) to place this automation in, or null for root.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>The trigger configuration.</summary>
    public TriggerConfiguration? Trigger { get; init; }

    /// <summary>The step configurations.</summary>
    public IList<StepConfiguration> Steps { get; init; } = [];

    /// <summary>The connections between steps.</summary>
    public IList<StepConnection> Connections { get; init; } = [];

    /// <summary>The serialised canvas state.</summary>
    public string? CanvasState { get; init; }

    /// <summary>Notification channel settings.</summary>
    public AutomationNotificationSettings? NotificationSettings { get; init; }

    /// <summary>
    /// The version of the automation that the client last read.
    /// Used for optimistic concurrency — the server returns 409 Conflict if this
    /// does not match the current version.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int Version { get; init; }
}
