using System.Text.Json.Serialization;
using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// An automation definition — a trigger plus a sequence of steps built visually on a canvas.
/// </summary>
public sealed class Automation : IPublishableEntity
{
    /// <inheritdoc />
    [JsonInclude]
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique, URL-safe alias (e.g. "publishSlackNotification").
    /// </summary>
    public required string Alias { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status.
    /// </summary>
    public AutomationStatus Status { get; set; } = AutomationStatus.Draft;

    /// <summary>
    /// Gets or sets the currently published version number, or null if never published.
    /// </summary>
    public int? PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets the workspace this automation belongs to.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the folder group ID, or null if at the root.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the trigger configuration.
    /// </summary>
    public TriggerConfiguration? Trigger { get; set; }

    /// <summary>
    /// Gets or sets the step configurations.
    /// </summary>
    public IList<StepConfiguration> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets the connections between steps.
    /// </summary>
    public IList<StepConnection> Connections { get; set; } = [];

    /// <summary>
    /// Gets or sets the notification settings for failure alerting.
    /// </summary>
    public AutomationNotificationSettings? NotificationSettings { get; set; }

    /// <summary>
    /// Gets or sets the serialised canvas state (viewport, layout).
    /// </summary>
    public string? CanvasState { get; set; }

    /// <inheritdoc />
    [JsonInclude]
    public int Version { get; internal set; } = 1;

    /// <inheritdoc />
    public DateTime DateCreated { get; init; } = DateTime.UtcNow;

    /// <inheritdoc />
    public DateTime DateModified { get; set; } = DateTime.UtcNow;

    /// <inheritdoc />
    public Guid? CreatedByUserId { get; set; }

    /// <inheritdoc />
    public Guid? ModifiedByUserId { get; set; }
}
