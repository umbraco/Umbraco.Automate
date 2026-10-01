using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Default implementation of <see cref="IWorkspaceService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class WorkspaceService : IWorkspaceService
{
    private const string EntityTypeName = "Workspace";

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceGroupRepository _groupRepository;
    private readonly IEntityVersionService _versionService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;
    private readonly IUserService _userService;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceGroupRepository groupRepository,
        IEntityVersionService versionService,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory,
        IUserService userService)
    {
        _workspaceRepository = workspaceRepository;
        _groupRepository = groupRepository;
        _versionService = versionService;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
        _userService = userService;
    }

    public Task<Workspace?> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default)
        => _workspaceRepository.GetAsync(id, cancellationToken);

    public Task<Workspace?> GetWorkspaceByAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _workspaceRepository.GetByAliasAsync(alias, cancellationToken);

    public Task<IEnumerable<Workspace>> GetAllWorkspacesAsync(CancellationToken cancellationToken = default)
        => _workspaceRepository.GetAllAsync(cancellationToken);

    public Task<(IEnumerable<Workspace> Items, int Total)> GetWorkspacesPagedAsync(
        string? filter = null,
        IReadOnlyCollection<Guid>? userGroupKeys = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _workspaceRepository.GetPagedAsync(filter, userGroupKeys, skip, take, cancellationToken);

    public async Task<Workspace> CreateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (workspace.Id == Guid.Empty)
        {
            workspace.Id = Guid.NewGuid();
        }

        await ValidateServiceAccountAsync(workspace);

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new WorkspaceSavingNotification(workspace, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Workspace creation was cancelled by a notification handler.");
        }

        var saved = await _workspaceRepository.SaveAsync(workspace, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, "Created", cancellationToken);

        scope.Notifications.Publish(new WorkspaceSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Workspace> UpdateWorkspaceAsync(Workspace workspace, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        await ValidateServiceAccountAsync(workspace);

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new WorkspaceSavingNotification(workspace, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Workspace update was cancelled by a notification handler.");
        }

        var saved = await _workspaceRepository.SaveAsync(workspace, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, cancellationToken: cancellationToken);

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

        await _groupRepository.DeleteByWorkspaceAsync(id, cancellationToken);
        await _versionService.DeleteVersionsAsync(id, EntityTypeName, cancellationToken);

        var deleted = await _workspaceRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new WorkspaceDeletedNotification(workspace, eventMessages));
        }

        scope.Complete();
        return deleted;
    }

    public async Task<Workspace> RollbackWorkspaceAsync(
        Guid workspaceId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _versionService.GetVersionSnapshotAsync<Workspace>(
            workspaceId, targetVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Version {targetVersion} not found for workspace '{workspaceId}'.");

        var current = await _workspaceRepository.GetAsync(workspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace '{workspaceId}' not found.");

        current.Name = snapshot.Name;
        current.Alias = snapshot.Alias;
        current.ServiceAccountKey = snapshot.ServiceAccountKey;
        current.UserGroups = snapshot.UserGroups;
        current.AllowedConnections = snapshot.AllowedConnections;

        return await UpdateWorkspaceAsync(current, userId, cancellationToken);
    }

    public Task<IReadOnlySet<Guid>> GetAccessibleWorkspaceIdsAsync(
        IEnumerable<Guid> userGroupKeys,
        CancellationToken cancellationToken = default)
        => _workspaceRepository.GetIdsByUserGroupKeysAsync(userGroupKeys, cancellationToken);

    /// <summary>
    /// Rejects a service account that is not an existing API user. Automations run as this user, so a
    /// person's account would tie them to that person's permissions and lifecycle. A workspace without
    /// a service account is allowed; it just can't run anything that needs one.
    /// </summary>
    /// <remarks>
    /// Checked on every save, so a workspace saved before this rule existed keeps running untouched
    /// until someone next saves it.
    /// </remarks>
    private async Task ValidateServiceAccountAsync(Workspace workspace)
    {
        if (workspace.ServiceAccountKey == Guid.Empty)
        {
            return;
        }

        var user = await _userService.GetAsync(workspace.ServiceAccountKey);
        if (user is null)
        {
            throw new WorkspaceServiceAccountValidationException(
                "The service account could not be found. Choose an existing API user.");
        }

        if (user.Kind != UserKind.Api)
        {
            throw new WorkspaceServiceAccountValidationException(
                $"The service account must be an API user, but '{user.Name}' is a regular backoffice user. Choose an API user, or create one in the Users section.");
        }
    }
}
