using System.Collections.Concurrent;
using Newtonsoft.Json;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// In-memory <see cref="IPersistenceProvider"/> for WorkflowCore.
/// This is the initial implementation — a later iteration will replace it
/// with an EF Core-backed provider using Umbraco's <c>IEFCoreScopeProvider</c>.
/// </summary>
/// <remarks>
/// Modelled on WorkflowCore's <c>MemoryPersistenceProvider</c> (~225 lines).
/// Uses deep-clone via JSON round-trip to prevent mutation of stored instances.
/// </remarks>
internal sealed class InMemoryWorkflowPersistenceProvider : IPersistenceProvider
{
    private readonly ConcurrentDictionary<string, string> _workflows = new();
    private readonly ConcurrentDictionary<string, string> _subscriptions = new();
    private readonly ConcurrentDictionary<string, string> _events = new();
    private readonly ConcurrentDictionary<string, string> _scheduledCommands = new();
    private readonly ConcurrentBag<ExecutionError> _errors = [];

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    // === IPersistenceProvider (direct) ===

    public void EnsureStoreExists()
    {
        // No-op for in-memory.
    }

    public Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken)
    {
        foreach (var error in errors)
        {
            _errors.Add(error);
        }

        return Task.CompletedTask;
    }

    // === IWorkflowRepository ===

    public Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(workflow.Id))
        {
            workflow.Id = Guid.NewGuid().ToString();
        }

        _workflows[workflow.Id] = Serialize(workflow);
        return Task.FromResult(workflow.Id);
    }

    public Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        _workflows[workflow.Id] = Serialize(workflow);
        return Task.CompletedTask;
    }

    public Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken)
    {
        _workflows[workflow.Id] = Serialize(workflow);
        foreach (var sub in subscriptions)
        {
            _subscriptions[sub.Id] = Serialize(sub);
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken)
    {
        var result = _workflows.Values
            .Select(Deserialize<WorkflowInstance>)
            .Where(w => w is not null && w.Status == WorkflowStatus.Runnable && w.NextExecution <= asAt.Ticks)
            .Select(w => w!.Id)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(result);
    }

    public Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(
        WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
    {
        var query = _workflows.Values
            .Select(Deserialize<WorkflowInstance>)
            .Where(w => w is not null)
            .Select(w => w!);

        if (status.HasValue)
        {
            query = query.Where(w => w.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(w => w.WorkflowDefinitionId == type);
        }

        if (createdFrom.HasValue)
        {
            query = query.Where(w => w.CreateTime >= createdFrom.Value);
        }

        if (createdTo.HasValue)
        {
            query = query.Where(w => w.CreateTime <= createdTo.Value);
        }

        var result = query.Skip(skip).Take(take).ToList();
        return Task.FromResult<IEnumerable<WorkflowInstance>>(result);
    }

    public Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken)
    {
        if (_workflows.TryGetValue(id, out var json))
        {
            return Task.FromResult(Deserialize<WorkflowInstance>(json)!);
        }

        throw new InvalidOperationException($"Workflow instance '{id}' not found.");
    }

    public Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var result = ids
            .Where(id => _workflows.ContainsKey(id))
            .Select(id => Deserialize<WorkflowInstance>(_workflows[id])!)
            .ToList();

        return Task.FromResult<IEnumerable<WorkflowInstance>>(result);
    }

    // === ISubscriptionRepository ===

    public Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subscription.Id))
        {
            subscription.Id = Guid.NewGuid().ToString();
        }

        _subscriptions[subscription.Id] = Serialize(subscription);
        return Task.FromResult(subscription.Id);
    }

    public Task<IEnumerable<EventSubscription>> GetSubscriptions(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        var result = _subscriptions.Values
            .Select(Deserialize<EventSubscription>)
            .Where(s => s is not null
                        && s.EventName == eventName
                        && s.EventKey == eventKey
                        && s.SubscribeAsOf <= asOf
                        && s.ExternalToken == null)
            .Select(s => s!)
            .ToList();

        return Task.FromResult<IEnumerable<EventSubscription>>(result);
    }

    public Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        _subscriptions.TryRemove(eventSubscriptionId, out _);
        return Task.CompletedTask;
    }

    public Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        if (_subscriptions.TryGetValue(eventSubscriptionId, out var json))
        {
            return Task.FromResult(Deserialize<EventSubscription>(json)!);
        }

        throw new InvalidOperationException($"Subscription '{eventSubscriptionId}' not found.");
    }

    public Task<EventSubscription> GetFirstOpenSubscription(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        var result = _subscriptions.Values
            .Select(Deserialize<EventSubscription>)
            .FirstOrDefault(s => s is not null
                                 && s.EventName == eventName
                                 && s.EventKey == eventKey
                                 && s.SubscribeAsOf <= asOf
                                 && s.ExternalToken == null);

        return Task.FromResult(result!);
    }

    public Task<bool> SetSubscriptionToken(
        string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken)
    {
        if (_subscriptions.TryGetValue(eventSubscriptionId, out var json))
        {
            var sub = Deserialize<EventSubscription>(json)!;
            sub.ExternalToken = token;
            sub.ExternalWorkerId = workerId;
            sub.ExternalTokenExpiry = expiry;
            _subscriptions[eventSubscriptionId] = Serialize(sub);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken)
    {
        if (_subscriptions.TryGetValue(eventSubscriptionId, out var json))
        {
            var sub = Deserialize<EventSubscription>(json)!;
            if (sub.ExternalToken == token)
            {
                sub.ExternalToken = null;
                sub.ExternalWorkerId = null;
                sub.ExternalTokenExpiry = null;
                _subscriptions[eventSubscriptionId] = Serialize(sub);
            }
        }

        return Task.CompletedTask;
    }

    // === IEventRepository ===

    public Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(newEvent.Id))
        {
            newEvent.Id = Guid.NewGuid().ToString();
        }

        _events[newEvent.Id] = Serialize(newEvent);
        return Task.FromResult(newEvent.Id);
    }

    public Task<Event> GetEvent(string id, CancellationToken cancellationToken)
    {
        if (_events.TryGetValue(id, out var json))
        {
            return Task.FromResult(Deserialize<Event>(json)!);
        }

        throw new InvalidOperationException($"Event '{id}' not found.");
    }

    public Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken)
    {
        var result = _events.Values
            .Select(Deserialize<Event>)
            .Where(e => e is not null && !e.IsProcessed && e.EventTime <= asAt)
            .Select(e => e!.Id)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(result);
    }

    public Task<IEnumerable<string>> GetEvents(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        var result = _events.Values
            .Select(Deserialize<Event>)
            .Where(e => e is not null
                        && e.EventName == eventName
                        && e.EventKey == eventKey
                        && e.EventTime >= asOf)
            .Select(e => e!.Id)
            .ToList();

        return Task.FromResult<IEnumerable<string>>(result);
    }

    public Task MarkEventProcessed(string id, CancellationToken cancellationToken)
    {
        if (_events.TryGetValue(id, out var json))
        {
            var evt = Deserialize<Event>(json)!;
            evt.IsProcessed = true;
            _events[id] = Serialize(evt);
        }

        return Task.CompletedTask;
    }

    public Task MarkEventUnprocessed(string id, CancellationToken cancellationToken)
    {
        if (_events.TryGetValue(id, out var json))
        {
            var evt = Deserialize<Event>(json)!;
            evt.IsProcessed = false;
            _events[id] = Serialize(evt);
        }

        return Task.CompletedTask;
    }

    // === IScheduledCommandRepository ===

    public bool SupportsScheduledCommands => true;

    public Task ScheduleCommand(ScheduledCommand command)
    {
        var key = $"{command.CommandName}:{command.Data}";
        _scheduledCommands[key] = Serialize(command);
        return Task.CompletedTask;
    }

    public async Task ProcessCommands(
        DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken)
    {
        var due = _scheduledCommands.Values
            .Select(Deserialize<ScheduledCommand>)
            .Where(c => c is not null && c.ExecuteTime <= asOf.UtcTicks)
            .ToList();

        foreach (var command in due)
        {
            await action(command!);
            var key = $"{command!.CommandName}:{command.Data}";
            _scheduledCommands.TryRemove(key, out _);
        }
    }

    // === Serialization helpers ===

    private static string Serialize<T>(T obj) =>
        JsonConvert.SerializeObject(obj, JsonSettings);

    private static T? Deserialize<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json, JsonSettings);
}
