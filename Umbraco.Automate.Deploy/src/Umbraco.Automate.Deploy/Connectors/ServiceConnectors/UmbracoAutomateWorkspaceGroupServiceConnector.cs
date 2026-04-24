using System.Runtime.CompilerServices;
using Umbraco.Automate.Core.Automations;
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
    IAutomationService automationService,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateWorkspaceGroupArtifact, WorkspaceGroup>(settingsAccessor)
{
    /// <inheritdoc />
    /// <remarks>
    /// Two passes to handle nested folders: Deploy processes artifacts within a pass in
    /// package order, not dependency order, so a child group can appear before its parent.
    /// Pass 4 creates/updates every group at the root; pass 5 re-parents once every group
    /// in the package exists.
    /// </remarks>
    protected override int[] ProcessPasses => [4, 5];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors =>
    [
        Constants.DeploySelector.This,
        Constants.DeploySelector.ThisAndDescendants,
        Constants.DeploySelector.DescendantsOfThis,
    ];

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
            case 4:
                await Pass4Async(state, cancellationToken);
                break;
            case 5:
                await Pass5Async(state, cancellationToken);
                break;
        }
    }

    private async Task Pass4Async(
        ArtifactDeployState<AutomateWorkspaceGroupArtifact, WorkspaceGroup> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        artifact.WorkspaceUdi.EnsureType(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);

        // Save every group at the root first. The final parent is applied in pass 5
        // once every group in the package exists, so intra-pass ordering doesn't matter.
        var group = state.Entity ?? new WorkspaceGroup
        {
            Id = artifact.Udi.Guid,
            Name = artifact.Name,
        };
        group.Name = artifact.Name;
        group.WorkspaceId = artifact.WorkspaceUdi.Guid;
        group.ParentId = null;

        state.Entity = await groupService.SaveGroupForDeployAsync(group, cancellationToken);
    }

    private async Task Pass5Async(
        ArtifactDeployState<AutomateWorkspaceGroupArtifact, WorkspaceGroup> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        if (artifact.ParentUdi is null)
        {
            return;
        }

        artifact.ParentUdi.EnsureType(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup);

        var group = state.Entity!;
        group.ParentId = artifact.ParentUdi.Guid;

        state.Entity = await groupService.SaveGroupForDeployAsync(group, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Direct children of a group are its sub-groups and the automations assigned
    /// to this group.
    /// </remarks>
    protected override async IAsyncEnumerable<GuidUdi> GetChildUdisAsync(
        WorkspaceGroup entity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var childGroups = await groupService.GetGroupsByWorkspaceAsync(entity.WorkspaceId, entity.Id, cancellationToken);
        foreach (var group in childGroups)
        {
            yield return new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, group.Id);
        }

        var (automations, _) = await automationService.GetAutomationsPagedAsync(
            workspaceIds: new HashSet<Guid> { entity.WorkspaceId },
            groupId: entity.Id,
            skip: 0,
            take: int.MaxValue,
            cancellationToken: cancellationToken);

        foreach (var automation in automations)
        {
            yield return new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Descendants of a group are every nested group and every automation that lives
    /// anywhere within the group's subtree.
    /// </remarks>
    protected override async IAsyncEnumerable<GuidUdi> GetDescendantUdisAsync(
        WorkspaceGroup entity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var allGroupsInWorkspace = (await groupService.GetAllGroupsAsync(cancellationToken))
            .Where(g => g.WorkspaceId == entity.WorkspaceId)
            .ToList();

        var descendantGroupIds = CollectDescendantGroupIds(entity.Id, allGroupsInWorkspace);

        foreach (var groupId in descendantGroupIds)
        {
            yield return new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, groupId);
        }

        // Automations directly under this group OR any descendant group.
        var scopeGroupIds = new HashSet<Guid>(descendantGroupIds) { entity.Id };
        var (allAutomations, _) = await automationService.GetAutomationsPagedAsync(
            workspaceIds: new HashSet<Guid> { entity.WorkspaceId },
            groupId: null,
            skip: 0,
            take: int.MaxValue,
            cancellationToken: cancellationToken);

        foreach (var automation in allAutomations)
        {
            if (automation.GroupId.HasValue && scopeGroupIds.Contains(automation.GroupId.Value))
            {
                yield return new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Automation, automation.Id);
            }
        }
    }

    private static List<Guid> CollectDescendantGroupIds(Guid rootId, IReadOnlyCollection<WorkspaceGroup> allGroups)
    {
        var result = new List<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var parentId = queue.Dequeue();
            foreach (var group in allGroups.Where(g => g.ParentId == parentId))
            {
                result.Add(group.Id);
                queue.Enqueue(group.Id);
            }
        }

        return result;
    }
}
