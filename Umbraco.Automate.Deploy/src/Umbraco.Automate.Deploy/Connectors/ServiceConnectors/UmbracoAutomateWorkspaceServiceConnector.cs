using System.Runtime.CompilerServices;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Service connector for Automate Workspaces, responsible for synchronizing
/// workspace entities during deploy operations. Resolves Connection dependencies.
/// </summary>
[UdiDefinition(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, UdiType.GuidUdi)]
public class UmbracoAutomateWorkspaceServiceConnector(
    IWorkspaceService workspaceService,
    IConnectionService connectionService,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateWorkspaceArtifact, Workspace>(settingsAccessor)
{
    /// <inheritdoc />
    protected override int[] ProcessPasses => [3];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors => ["this", "this-and-descendants", "descendants"];

    /// <inheritdoc />
    protected override string OpenUdiName => "All Umbraco Automate Workspaces";

    /// <inheritdoc />
    public override string UdiEntityType => UmbracoAutomateDeployConstants.UdiEntityType.Workspace;

    /// <inheritdoc />
    public override async Task<Workspace?> GetEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => await workspaceService.GetWorkspaceAsync(id, cancellationToken);

    /// <inheritdoc />
    public override async IAsyncEnumerable<Workspace> GetEntitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var workspaces = await workspaceService.GetAllWorkspacesAsync(cancellationToken);
        foreach (var workspace in workspaces)
        {
            yield return workspace;
        }
    }

    /// <inheritdoc />
    public override string GetEntityName(Workspace entity) => entity.Name;

    /// <inheritdoc />
    public override Task<AutomateWorkspaceArtifact?> GetArtifactAsync(
        GuidUdi udi,
        Workspace? entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult<AutomateWorkspaceArtifact?>(null);
        }

        var dependencies = new ArtifactDependencyCollection();

        // Add Connection dependencies
        var connectionUdis = new List<GuidUdi>();
        foreach (var connectionId in entity.AllowedConnections)
        {
            var connectionUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connectionId);
            dependencies.Add(new UmbracoAutomateArtifactDependency(connectionUdi, ArtifactDependencyMode.Match));
            connectionUdis.Add(connectionUdi);
        }

        var artifact = new AutomateWorkspaceArtifact(udi, dependencies)
        {
            Alias = entity.Alias,
            Name = entity.Name,
            ServiceAccountKey = entity.ServiceAccountKey,
            UserGroups = entity.UserGroups.ToList(),
            AllowedConnectionUdis = connectionUdis,
        };

        return Task.FromResult<AutomateWorkspaceArtifact?>(artifact);
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(
        ArtifactDeployState<AutomateWorkspaceArtifact, Workspace> state,
        IDeployContext context,
        int pass,
        CancellationToken cancellationToken = default)
    {
        state.NextPass = GetNextPass(pass);

        switch (pass)
        {
            case 3:
                await Pass3Async(state, cancellationToken);
                break;
        }
    }

    private async Task Pass3Async(
        ArtifactDeployState<AutomateWorkspaceArtifact, Workspace> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        // Resolve AllowedConnection UDIs back to IDs
        var allowedConnectionIds = new List<Guid>();
        foreach (var connectionUdi in artifact.AllowedConnectionUdis)
        {
            connectionUdi.EnsureType(UmbracoAutomateDeployConstants.UdiEntityType.Connection);

            var connection = await connectionService.GetConnectionAsync(connectionUdi.Guid, cancellationToken);
            if (connection != null)
            {
                allowedConnectionIds.Add(connection.Id);
            }
        }

        if (state.Entity != null)
        {
            // Update existing workspace
            var workspace = state.Entity;
            workspace.Alias = artifact.Alias!;
            workspace.Name = artifact.Name;
            workspace.ServiceAccountKey = artifact.ServiceAccountKey;
            workspace.UserGroups = artifact.UserGroups.ToList();
            workspace.AllowedConnections = allowedConnectionIds;

            state.Entity = await workspaceService.UpdateWorkspaceAsync(workspace, cancellationToken: cancellationToken);
        }
        else
        {
            // Create new workspace
            var workspace = new Workspace
            {
                Alias = artifact.Alias!,
                Name = artifact.Name,
                ServiceAccountKey = artifact.ServiceAccountKey,
                UserGroups = artifact.UserGroups.ToList(),
                AllowedConnections = allowedConnectionIds,
            };

            state.Entity = await workspaceService.CreateWorkspaceAsync(workspace, cancellationToken: cancellationToken);
        }
    }
}
