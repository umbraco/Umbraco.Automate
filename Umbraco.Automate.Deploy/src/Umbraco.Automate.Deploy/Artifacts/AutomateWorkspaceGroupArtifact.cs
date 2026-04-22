using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Deploy.Infrastructure.Artifacts;

namespace Umbraco.Automate.Deploy.Artifacts;

/// <summary>
/// Represents a deployment artifact for a workspace group (folder).
/// </summary>
public class AutomateWorkspaceGroupArtifact(GuidUdi udi, IEnumerable<ArtifactDependency>? dependencies = null)
    : DeployArtifactBase<GuidUdi>(udi, dependencies)
{
    /// <summary>
    /// UDI of the workspace this group belongs to.
    /// </summary>
    public required GuidUdi WorkspaceUdi { get; set; }

    /// <summary>
    /// UDI of the parent group, or <c>null</c> when this group is at the root of its workspace.
    /// </summary>
    public GuidUdi? ParentUdi { get; set; }
}
