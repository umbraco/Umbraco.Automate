using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Deploy.Infrastructure.Artifacts;

namespace Umbraco.Automate.Deploy.Artifacts;

/// <summary>
/// Represents a deployment artifact for an Automate connection.
/// </summary>
public class AutomateConnectionArtifact(GuidUdi udi, IEnumerable<ArtifactDependency>? dependencies = null)
    : DeployArtifactBase<GuidUdi>(udi, dependencies)
{
    /// <summary>
    /// The connection type alias (e.g. "httpBasicAuth", "oauth2").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// The connection settings serialized as JSON, with sensitive values filtered based on deploy settings.
    /// </summary>
    public JsonElement? Settings { get; set; }
}
