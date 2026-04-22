using System.Runtime.CompilerServices;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Service connector for workspace groups (folders). Resolves Workspace and parent-group dependencies
/// so nested groups deploy in the correct order.
/// </summary>
[UdiDefinition(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, UdiType.GuidUdi)]
public class UmbracoAutomateWorkspaceGroupServiceConnector(
    IWorkspaceGroupService groupService,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateWorkspaceGroupArtifact, WorkspaceGroup>(settingsAccessor)
{
    /// <inheritdoc />
    protected override int[] ProcessPasses => [3];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors => ["this", "this-and-descendants", "descendants"];

    /// <inheritdoc />
    protected override string OpenUdiName => "All Umbraco Automate Workspace Groups";

    /// <inheritdoc />
    public override string UdiEntityType => UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup;

    /// <inheritdoc />
    public override async Task<WorkspaceGroup?> GetEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => await groupService.GetGroupAsync(id, cancellationToken);

    /// <inheritdoc />
    public override async IAsyncEnumerable<WorkspaceGroup> GetEntitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var groups = await groupService.GetAllGroupsAsync(cancellationToken);
        foreach (var group in groups)
        {
            yield return group;
        }
    }

    /// <inheritdoc />
    public override string GetEntityName(WorkspaceGroup entity) => entity.Name;

    /// <inheritdoc />
    public override Task<AutomateWorkspaceGroupArtifact?> GetArtifactAsync(
        GuidUdi udi,
        WorkspaceGroup? entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult<AutomateWorkspaceGroupArtifact?>(null);
        }

        var dependencies = new ArtifactDependencyCollection();

        // The owning workspace must exist in the target environment before this group.
        var workspaceUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, entity.WorkspaceId);
        dependencies.Add(new UmbracoAutomateArtifactDependency(workspaceUdi, ArtifactDependencyMode.Match));

        // Nested groups depend on their parent group being deployed first.
        GuidUdi? parentUdi = null;
        if (entity.ParentId.HasValue)
        {
            parentUdi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, entity.ParentId.Value);
            dependencies.Add(new UmbracoAutomateArtifactDependency(parentUdi, ArtifactDependencyMode.Match));
        }

        var artifact = new AutomateWorkspaceGroupArtifact(udi, dependencies)
        {
            Name = entity.Name,
            WorkspaceUdi = workspaceUdi,
            ParentUdi = parentUdi,
        };

        return Task.FromResult<AutomateWorkspaceGroupArtifact?>(artifact);
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(
        ArtifactDeployState<AutomateWorkspaceGroupArtifact, WorkspaceGroup> state,
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
        ArtifactDeployState<AutomateWorkspaceGroupArtifact, WorkspaceGroup> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        artifact.WorkspaceUdi.EnsureType(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);

        if (state.Entity != null)
        {
            // Update existing group — only Name and ParentId are mutable.
            var group = state.Entity;
            group.Name = artifact.Name;
            group.ParentId = artifact.ParentUdi?.Guid;

            state.Entity = await groupService.UpdateGroupAsync(group, cancellationToken);
        }
        else
        {
            // Create new group with the artifact's UDI as the entity Id to keep
            // redeployment idempotent across environments.
            var group = new WorkspaceGroup
            {
                Id = state.Artifact.Udi.Guid,
                Name = artifact.Name,
                WorkspaceId = artifact.WorkspaceUdi.Guid,
                ParentId = artifact.ParentUdi?.Guid,
            };

            state.Entity = await groupService.CreateGroupAsync(group, cancellationToken);
        }
    }
}
