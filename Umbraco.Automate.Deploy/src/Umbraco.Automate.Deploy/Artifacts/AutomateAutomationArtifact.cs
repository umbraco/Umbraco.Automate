using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Deploy.Infrastructure.Artifacts;

namespace Umbraco.Automate.Deploy.Artifacts;

/// <summary>
/// Represents a deployment artifact for an automation.
/// </summary>
public class AutomateAutomationArtifact(GuidUdi udi, IEnumerable<ArtifactDependency>? dependencies = null)
    : DeployArtifactBase<GuidUdi>(udi, dependencies)
{
    /// <summary>
    /// An optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// UDI of the workspace this automation belongs to.
    /// </summary>
    public required GuidUdi WorkspaceUdi { get; set; }

    /// <summary>
    /// The folder group ID, or null if at the root.
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// The trigger configuration serialized as JSON.
    /// </summary>
    public JsonElement? Trigger { get; set; }

    /// <summary>
    /// The step configurations serialized as JSON.
    /// </summary>
    public JsonElement? Steps { get; set; }

    /// <summary>
    /// The step connections serialized as JSON.
    /// </summary>
    public JsonElement? Connections { get; set; }

    /// <summary>
    /// The notification settings serialized as JSON.
    /// </summary>
    public JsonElement? NotificationSettings { get; set; }

    /// <summary>
    /// The serialised canvas state (viewport, layout).
    /// </summary>
    public string? CanvasState { get; set; }
}
