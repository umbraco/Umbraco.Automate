using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Default implementation of <see cref="IAutomationService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class AutomationService : IAutomationService
{
    private const string EntityTypeName = "Automation";

    private readonly IAutomationRepository _automationRepository;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IEntityVersionService _versionService;
    private readonly IWorkspaceService _workspaceService;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;

    public AutomationService(
        IAutomationRepository automationRepository,
        IAutomationRunRepository runRepository,
        IEntityVersionService versionService,
        IWorkspaceService workspaceService,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory)
    {
        _automationRepository = automationRepository;
        _runRepository = runRepository;
        _versionService = versionService;
        _workspaceService = workspaceService;
        _scopeProvider = scopeProvider;
        _eventMessagesFactory = eventMessagesFactory;
    }

    public Task<Automation?> GetAutomationAsync(Guid id, CancellationToken cancellationToken = default)
        => _automationRepository.GetAsync(id, cancellationToken);

    public Task<Automation?> GetAutomationByAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _automationRepository.GetByAliasAsync(alias, cancellationToken);

    public Task<IEnumerable<Automation>> GetAllAutomationsAsync(CancellationToken cancellationToken = default)
        => _automationRepository.GetAllAsync(cancellationToken);

    public Task<(IEnumerable<Automation> Items, int Total)> GetAutomationsPagedAsync(
        string? filter = null,
        IReadOnlySet<Guid>? workspaceIds = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _automationRepository.GetPagedAsync(filter, workspaceIds, skip, take, cancellationToken);

    public async Task<Automation> CreateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        if (automation.Id == Guid.Empty)
        {
            automation.Id = Guid.NewGuid();
        }

        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new AutomationSavingNotification(automation, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Automation creation was cancelled by a notification handler.");
        }

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, "Created", cancellationToken);

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Automation> UpdateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new AutomationSavingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(savingNotification))
        {
            throw new OperationCanceledException("Automation update was cancelled by a notification handler.");
        }

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        await _versionService.SaveVersionAsync(saved, userId, cancellationToken: cancellationToken);

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Automation> PublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{id}' not found.");

        // Publish-time validation.
        await ValidateForPublishAsync(automation, cancellationToken);

        var eventMessages = _eventMessagesFactory.Get();

        var publishingNotification = new AutomationPublishingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(publishingNotification))
        {
            throw new OperationCanceledException("Automation publish was cancelled by a notification handler.");
        }

        automation.PublishedVersion = automation.DraftVersion;
        automation.Status = AutomationStatus.Published;
        automation.IsEnabled = true;

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        scope.Notifications.Publish(new AutomationPublishedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    private async Task ValidateForPublishAsync(Automation automation, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // 1. Trigger must be configured.
        if (automation.Trigger is null)
        {
            errors.Add("A trigger must be configured before publishing.");
        }

        // 2. Workspace must exist and have a valid service account.
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken);
        if (workspace is null)
        {
            errors.Add($"Workspace '{automation.WorkspaceId}' not found.");
        }
        else
        {
            if (workspace.ServiceAccountKey == Guid.Empty)
            {
                errors.Add("The workspace's service account is not configured.");
            }

            // 3. All step connections must be allowed by the workspace.
            var stepConnectionIds = automation.Steps
                .Where(s => s.ConnectionId.HasValue)
                .Select(s => s.ConnectionId!.Value)
                .Distinct()
                .ToList();

            if (stepConnectionIds.Count > 0)
            {
                var allowedSet = workspace.AllowedConnections.ToHashSet();
                var disallowed = stepConnectionIds.Where(id => !allowedSet.Contains(id)).ToList();

                foreach (var connectionId in disallowed)
                {
                    errors.Add($"Connection '{connectionId}' is not allowed in workspace '{workspace.Name}'.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new AutomationValidationException(
                $"Cannot publish automation '{automation.Name}'.",
                errors);
        }
    }

    public async Task<Automation> UnpublishAutomationAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{id}' not found.");

        var eventMessages = _eventMessagesFactory.Get();

        var unpublishingNotification = new AutomationUnpublishingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(unpublishingNotification))
        {
            throw new OperationCanceledException("Automation unpublish was cancelled by a notification handler.");
        }

        automation.Status = AutomationStatus.Inactive;
        automation.IsEnabled = false;

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        scope.Notifications.Publish(new AutomationUnpublishedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<bool> DeleteAutomationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var automation = await _automationRepository.GetAsync(id, cancellationToken);
        if (automation is null)
        {
            return false;
        }

        var eventMessages = _eventMessagesFactory.Get();

        var deletingNotification = new AutomationDeletingNotification(automation, eventMessages);
        if (await scope.Notifications.PublishCancelableAsync(deletingNotification))
        {
            throw new OperationCanceledException("Automation deletion was cancelled by a notification handler.");
        }

        await _runRepository.DeleteByAutomationAsync(id, cancellationToken);
        await _versionService.DeleteVersionsAsync(id, EntityTypeName, cancellationToken);
        var deleted = await _automationRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new AutomationDeletedNotification(automation, eventMessages));
        }

        scope.Complete();
        return deleted;
    }

    public Task<IReadOnlyCollection<(Guid Id, int PublishedVersion)>> GetPublishedVersionReferencesAsync(
        CancellationToken cancellationToken = default)
        => _automationRepository.GetPublishedVersionReferencesAsync(cancellationToken);

    public Task<(IEnumerable<EntityVersion> Items, int Total)> GetAutomationVersionHistoryAsync(
        Guid automationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
        => _versionService.GetVersionHistoryAsync(automationId, EntityTypeName, skip, take, cancellationToken);

    public Task<Automation?> GetAutomationVersionSnapshotAsync(
        Guid automationId,
        int version,
        CancellationToken cancellationToken = default)
        => _versionService.GetVersionSnapshotAsync<Automation>(automationId, version, cancellationToken);

    public async Task<Automation> RollbackAutomationAsync(
        Guid automationId,
        int targetVersion,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _versionService.GetVersionSnapshotAsync<Automation>(
            automationId, targetVersion, cancellationToken)
            ?? throw new InvalidOperationException($"Version {targetVersion} not found for automation '{automationId}'.");

        // Rollback is a draft-only operation: restore content fields from the target version
        // into a new draft. Lifecycle state (PublishedVersion, Status, IsEnabled) is preserved
        // from the current entity — the user must explicitly re-publish if desired.
        var current = await _automationRepository.GetAsync(automationId, cancellationToken)
            ?? throw new InvalidOperationException($"Automation '{automationId}' not found.");

        current.Name = snapshot.Name;
        current.Alias = snapshot.Alias;
        current.Description = snapshot.Description;
        current.Trigger = snapshot.Trigger;
        current.Steps = snapshot.Steps;
        current.Connections = snapshot.Connections;
        current.CanvasState = snapshot.CanvasState;

        return await UpdateAutomationAsync(current, userId, cancellationToken);
    }
}
