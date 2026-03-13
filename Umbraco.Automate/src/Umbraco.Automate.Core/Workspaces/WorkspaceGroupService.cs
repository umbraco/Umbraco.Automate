using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Default implementation of <see cref="IWorkspaceGroupService"/>.
/// </summary>
internal sealed class WorkspaceGroupService : IWorkspaceGroupService
{
    private readonly IWorkspaceGroupRepository _groupRepository;
    private readonly IAutomationService _automationService;
    private readonly IAutomationRepository _automationRepository;
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceGroupService(
        IWorkspaceGroupRepository groupRepository,
        IAutomationService automationService,
        IAutomationRepository automationRepository,
        IWorkspaceService workspaceService)
    {
        _groupRepository = groupRepository;
        _automationService = automationService;
        _automationRepository = automationRepository;
        _workspaceService = workspaceService;
    }

    public Task<WorkspaceGroup?> GetGroupAsync(Guid id, CancellationToken cancellationToken = default)
        => _groupRepository.GetAsync(id, cancellationToken);

    public Task<IEnumerable<WorkspaceGroup>> GetGroupsByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => _groupRepository.GetByWorkspaceAsync(workspaceId, cancellationToken);

    public async Task<WorkspaceGroup> CreateGroupAsync(WorkspaceGroup group, CancellationToken cancellationToken = default)
    {
        if (group.Id == Guid.Empty)
        {
            group.Id = Guid.NewGuid();
        }

        await ValidateWorkspaceExistsAsync(group.WorkspaceId, cancellationToken);
        await ValidateParentAsync(group.ParentId, group.WorkspaceId, cancellationToken);
        await ValidateUniqueNameAsync(group.WorkspaceId, group.ParentId, group.Name, excludeId: null, cancellationToken);

        return await _groupRepository.SaveAsync(group, cancellationToken);
    }

    public async Task<WorkspaceGroup> UpdateGroupAsync(WorkspaceGroup group, CancellationToken cancellationToken = default)
    {
        var existing = await _groupRepository.GetAsync(group.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace group '{group.Id}' not found.");

        await ValidateParentAsync(group.ParentId, existing.WorkspaceId, cancellationToken);
        await ValidateUniqueNameAsync(existing.WorkspaceId, group.ParentId, group.Name, group.Id, cancellationToken);

        existing.Name = group.Name;
        existing.ParentId = group.ParentId;

        return await _groupRepository.SaveAsync(existing, cancellationToken);
    }

    public async Task<bool> DeleteGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _groupRepository.GetAsync(id, cancellationToken);
        if (group is null)
        {
            return false;
        }

        await CascadeDeleteAsync(id, cancellationToken);

        return true;
    }

    private async Task CascadeDeleteAsync(Guid groupId, CancellationToken cancellationToken)
    {
        // Delete child groups recursively.
        var childIds = await _groupRepository.GetChildIdsAsync(groupId, cancellationToken);
        foreach (var childId in childIds)
        {
            await CascadeDeleteAsync(childId, cancellationToken);
        }

        // Delete automations in this group.
        var (automations, _) = await _automationRepository.GetPagedAsync(
            groupId: groupId,
            skip: 0,
            take: int.MaxValue,
            cancellationToken: cancellationToken);

        foreach (var automation in automations)
        {
            await _automationService.DeleteAutomationAsync(automation.Id, cancellationToken);
        }

        // Delete the group itself.
        await _groupRepository.DeleteAsync(groupId, cancellationToken);
    }

    private async Task ValidateWorkspaceExistsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceService.GetWorkspaceAsync(workspaceId, cancellationToken);
        if (workspace is null)
        {
            throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");
        }
    }

    private async Task ValidateParentAsync(Guid? parentId, Guid workspaceId, CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return;
        }

        var parent = await _groupRepository.GetAsync(parentId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Parent group '{parentId}' not found.");

        if (parent.WorkspaceId != workspaceId)
        {
            throw new InvalidOperationException($"Parent group '{parentId}' belongs to a different workspace.");
        }
    }

    private async Task ValidateUniqueNameAsync(Guid workspaceId, Guid? parentId, string name, Guid? excludeId, CancellationToken cancellationToken)
    {
        var exists = await _groupRepository.NameExistsAsync(workspaceId, parentId, name, excludeId, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"A group named '{name}' already exists at this level.");
        }
    }
}
