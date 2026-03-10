using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Default implementation of <see cref="IAutomationService"/>.
/// Publishes lifecycle notifications and delegates to the repository.
/// </summary>
internal sealed class AutomationService : IAutomationService
{
    private readonly IAutomationRepository _automationRepository;
    private readonly IAutomationRunRepository _runRepository;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IEventMessagesFactory _eventMessagesFactory;

    public AutomationService(
        IAutomationRepository automationRepository,
        IAutomationRunRepository runRepository,
        ICoreScopeProvider scopeProvider,
        IEventMessagesFactory eventMessagesFactory)
    {
        _automationRepository = automationRepository;
        _runRepository = runRepository;
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
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
        => _automationRepository.GetPagedAsync(filter, skip, take, cancellationToken);

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

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
        scope.Complete();

        return saved;
    }

    public async Task<Automation> UpdateAutomationAsync(Automation automation, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        using ICoreScope scope = _scopeProvider.CreateCoreScope();

        var eventMessages = _eventMessagesFactory.Get();

        var savingNotification = new AutomationSavingNotification(automation, eventMessages);
        if (scope.Notifications.PublishCancelable(savingNotification))
        {
            throw new OperationCanceledException("Automation update was cancelled by a notification handler.");
        }

        var saved = await _automationRepository.SaveAsync(automation, userId, cancellationToken);

        scope.Notifications.Publish(new AutomationSavedNotification(saved, eventMessages));
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
        if (scope.Notifications.PublishCancelable(deletingNotification))
        {
            throw new OperationCanceledException("Automation deletion was cancelled by a notification handler.");
        }

        await _runRepository.DeleteByAutomationAsync(id, cancellationToken);
        var deleted = await _automationRepository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            scope.Notifications.Publish(new AutomationDeletedNotification(automation, eventMessages));
        }

        scope.Complete();
        return deleted;
    }
}
