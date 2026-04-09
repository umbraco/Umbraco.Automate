using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy;

/// <summary>
/// Represents an artifact dependency for Umbraco.Automate entities.
/// Automatically sets checksum validation to false for Automate entity dependencies.
/// </summary>
public class UmbracoAutomateArtifactDependency(
    Udi udi,
    ArtifactDependencyMode mode = ArtifactDependencyMode.Exist)
    : ArtifactDependency(udi, false, mode);
