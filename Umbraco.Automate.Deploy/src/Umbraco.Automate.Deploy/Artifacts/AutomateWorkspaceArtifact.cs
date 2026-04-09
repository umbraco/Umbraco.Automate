using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Deploy.Infrastructure.Artifacts;

namespace Umbraco.Automate.Deploy.Artifacts;

/// <summary>
/// Represents a deployment artifact for an Automate workspace.
/// </summary>
public class AutomateWorkspaceArtifact(GuidUdi udi, IEnumerable<ArtifactDependency>? dependencies = null)
    : DeployArtifactBase<GuidUdi>(udi, dependencies)
{
    /// <summary>
    /// The service account key (UserKind.Api user) tied to this workspace.
    /// </summary>
    public Guid ServiceAccountKey { get; set; }

    /// <summary>
    /// User group keys that have access to this workspace.
    /// Stored as raw GUIDs since these are CMS-level entities.
    /// </summary>
    public IList<Guid> UserGroups { get; set; } = [];

    /// <summary>
    /// UDIs of connections allowed in this workspace.
    /// </summary>
    public IList<GuidUdi> AllowedConnectionUdis { get; set; } = [];
}
