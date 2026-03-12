using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Default implementation of <see cref="IWorkspaceService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory)
    {
        _workspaceRepository = workspaceRepository;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
    }

    public Task<Workspace?> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default)
        => _workspaceRepository.GetAsync(id, cancellationToken);

    public Task<Workspace?> GetWorkspaceByAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _workspaceRepository.GetByAliasAsync(alias, cancellationToken);

    public Task<IEnumerable<Workspace>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default)
        => _workspaceRepository.GetAllAsync(cancellationToken);

    public Task<(IEnumerable<Workspace> Items, int Total)> GetWorkspacesPagedAsync(
        string? filter = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _workspaceRepository.GetPagedAsync(filter, skip, take, cancellationToken);

    public async Task<Workspace> CreateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (workspace.Id == Guid.Empty)
        {
            workspace.Id = Guid.NewGuid();
        }

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new WorkspaceSavingNotification(workspace, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Workspace creation was cancelled by a notification handler.");
        }

        var saved = await _workspaceRepository.SaveAsync(workspace, userId, cancellationToken);

        scope.Notifications.Publish(new WorkspaceSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Workspace> UpdateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new WorkspaceSavingNotification(workspace, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Workspace update was cancelled by a notification handler.");
        }

        var saved = await _workspaceRepository.SaveAsync(workspace, userId, cancellationToken);

        scope.Notifications.Publish(new WorkspaceSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<bool> DeleteWorkspaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var workspace = await _workspaceRepository.GetAsync(id, cancellationToken);
        if (workspace is null)
        {
            return false;
        }

        var eventMessages = _eventMessagesFactory.Get();

        var deletingNotification = new WorkspaceDeletingNotification(workspace, eventMessages);
        if (scope.Notifications.PublishCancelable(deletingNotification))
        {
            throw new OperationCanceledException("Workspace deletion was cancelled by a notification handler.");
        }

        var deleted = await _workspaceRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new WorkspaceDeletedNotification(workspace, eventMessages));
        }

        scope.Complete();
        return deleted;
    }

    public Task<IReadOnlySet<Guid>> GetAccessibleWorkspaceIdsAsync(
        IEnumerable<Guid> userGroupKeys,
        CancellationToken cancellationToken = default)
        => _workspaceRepository.GetIdsByUserGroupKeysAsync(userGroupKeys, cancellationToken);
}
