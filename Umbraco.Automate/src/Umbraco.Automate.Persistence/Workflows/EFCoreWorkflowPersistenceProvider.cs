using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Umbraco.Cms.Persistence.EFCore.Scoping;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core-backed <see cref="IPersistenceProvider"/> for WorkflowCore.
/// Uses Umbraco's <see cref="IEFCoreScopeProvider{T}"/> for database access.
/// </summary>
internal sealed class EFCoreWorkflowPersistenceProvider : IPersistenceProvider
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    public EFCoreWorkflowPersistenceProvider(IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    // === IPersistenceProvider ===

    public void EnsureStoreExists()
    {
        // Migrations handle schema creation.
    }

    public async Task PersistErrors(IEnumerable<ExecutionError> errors, CancellationToken cancellationToken)
    {
        // Execution errors are logged but not persisted to a dedicated table.
        // Step-level errors are captured via StepRun records.
        await Task.CompletedTask;
    }

    // === IWorkflowRepository ===

    public async Task<string> CreateNewWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(workflow.Id))
        {
            workflow.Id = Guid.NewGuid().ToString();
        }

        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            db.WorkflowInstances.Add(ToEntity(workflow));
            await db.SaveChangesAsync(cancellationToken);
        });

        scope.Complete();
        return workflow.Id;
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.WorkflowInstances.FindAsync([workflow.Id], cancellationToken);
            if (entity is not null)
            {
                UpdateEntity(entity, workflow);
                await db.SaveChangesAsync(cancellationToken);
            }
        });

        scope.Complete();
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.WorkflowInstances.FindAsync([workflow.Id], cancellationToken);
            if (entity is not null)
            {
                UpdateEntity(entity, workflow);
            }

            foreach (var sub in subscriptions)
            {
                if (string.IsNullOrEmpty(sub.Id))
                {
                    sub.Id = Guid.NewGuid().ToString();
                }

                db.EventSubscriptions.Add(ToEntity(sub));
            }

            await db.SaveChangesAsync(cancellationToken);
        });

        scope.Complete();
    }

    public async Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.WorkflowInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            return entity is not null ? ToDomain(entity) : null;
        });

        scope.Complete();
        return result ?? throw new InvalidOperationException($"Workflow instance '{id}' not found.");
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entities = await db.WorkflowInstances
                .AsNoTracking()
                .Where(e => idList.Contains(e.Id))
                .ToListAsync(cancellationToken);

            return entities.Select(ToDomain).ToList();
        });

        scope.Complete();
        return result;
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(
        WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            IQueryable<WorkflowInstanceEntity> query = db.WorkflowInstances.AsNoTracking();

            if (status.HasValue)
            {
                var statusInt = (int)status.Value;
                query = query.Where(e => e.Status == statusInt);
            }

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(e => e.WorkflowDefinitionId == type);
            }

            if (createdFrom.HasValue)
            {
                query = query.Where(e => e.CreateTime >= createdFrom.Value);
            }

            if (createdTo.HasValue)
            {
                query = query.Where(e => e.CreateTime <= createdTo.Value);
            }

            var entities = await query.Skip(skip).Take(take).ToListAsync();
            return entities.Select(ToDomain).ToList();
        });

        scope.Complete();
        return result;
    }

    public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken)
    {
        var ticks = asAt.Ticks;
        var runnableStatus = (int)WorkflowStatus.Runnable;

        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
            await db.WorkflowInstances
                .AsNoTracking()
                .Where(e => e.Status == runnableStatus && e.NextExecution.HasValue && e.NextExecution <= ticks)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return result;
    }

    // === ISubscriptionRepository ===

    public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subscription.Id))
        {
            subscription.Id = Guid.NewGuid().ToString();
        }

        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            db.EventSubscriptions.Add(ToEntity(subscription));
            await db.SaveChangesAsync(cancellationToken);
        });

        scope.Complete();
        return subscription.Id;
    }

    public async Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.EventSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventSubscriptionId, cancellationToken);

            return entity is not null ? ToDomain(entity) : null;
        });

        scope.Complete();
        return result ?? throw new InvalidOperationException($"Subscription '{eventSubscriptionId}' not found.");
    }

    public async Task<EventSubscription> GetFirstOpenSubscription(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.EventSubscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.EventName == eventName
                    && e.EventKey == eventKey
                    && e.SubscribeAsOf <= asOf
                    && e.ExternalToken == null,
                    cancellationToken);

            return entity is not null ? ToDomain(entity) : null;
        });

        scope.Complete();
        return result!;
    }

    public async Task<IEnumerable<EventSubscription>> GetSubscriptions(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entities = await db.EventSubscriptions
                .AsNoTracking()
                .Where(e =>
                    e.EventName == eventName
                    && e.EventKey == eventKey
                    && e.SubscribeAsOf <= asOf
                    && e.ExternalToken == null)
                .ToListAsync(cancellationToken);

            return entities.Select(ToDomain).ToList();
        });

        scope.Complete();
        return result;
    }

    public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.EventSubscriptions.FindAsync([eventSubscriptionId], cancellationToken);
            if (entity is not null)
            {
                db.EventSubscriptions.Remove(entity);
                await db.SaveChangesAsync(cancellationToken);
            }
        });

        scope.Complete();
    }

    public async Task<bool> SetSubscriptionToken(
        string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.EventSubscriptions.FindAsync([eventSubscriptionId], cancellationToken);
            if (entity is null)
            {
                return false;
            }

            entity.ExternalToken = token;
            entity.ExternalWorkerId = workerId;
            entity.ExternalTokenExpiry = expiry;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        });

        scope.Complete();
        return result;
    }

    public async Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.EventSubscriptions.FindAsync([eventSubscriptionId], cancellationToken);
            if (entity is not null && entity.ExternalToken == token)
            {
                entity.ExternalToken = null;
                entity.ExternalWorkerId = null;
                entity.ExternalTokenExpiry = null;
                await db.SaveChangesAsync(cancellationToken);
            }
        });

        scope.Complete();
    }

    // === IEventRepository ===

    public async Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(newEvent.Id))
        {
            newEvent.Id = Guid.NewGuid().ToString();
        }

        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            db.Events.Add(ToEntity(newEvent));
            await db.SaveChangesAsync(cancellationToken);
        });

        scope.Complete();
        return newEvent.Id;
    }

    public async Task<Event> GetEvent(string id, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
        {
            var entity = await db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

            return entity is not null ? ToDomain(entity) : null;
        });

        scope.Complete();
        return result ?? throw new InvalidOperationException($"Event '{id}' not found.");
    }

    public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
            await db.Events
                .AsNoTracking()
                .Where(e => !e.IsProcessed && e.EventTime <= asAt)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return result;
    }

    public async Task<IEnumerable<string>> GetEvents(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        var result = await scope.ExecuteWithContextAsync(async db =>
            await db.Events
                .AsNoTracking()
                .Where(e => e.EventName == eventName && e.EventKey == eventKey && e.EventTime >= asOf)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken));

        scope.Complete();
        return result;
    }

    public async Task MarkEventProcessed(string id, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.Events.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                entity.IsProcessed = true;
                await db.SaveChangesAsync(cancellationToken);
            }
        });

        scope.Complete();
    }

    public async Task MarkEventUnprocessed(string id, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var entity = await db.Events.FindAsync([id], cancellationToken);
            if (entity is not null)
            {
                entity.IsProcessed = false;
                await db.SaveChangesAsync(cancellationToken);
            }
        });

        scope.Complete();
    }

    // === IScheduledCommandRepository ===

    public bool SupportsScheduledCommands => true;

    public async Task ScheduleCommand(ScheduledCommand command)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            db.ScheduledCommands.Add(new ScheduledCommandEntity
            {
                CommandName = command.CommandName,
                Data = command.Data,
                ExecuteTime = new DateTime(command.ExecuteTime, DateTimeKind.Utc),
            });
            await db.SaveChangesAsync();
        });

        scope.Complete();
    }

    public async Task ProcessCommands(
        DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<UmbracoAutomateDbContext>(async db =>
        {
            var asOfUtc = asOf.UtcDateTime;
            var due = await db.ScheduledCommands
                .Where(c => c.ExecuteTime <= asOfUtc)
                .ToListAsync(cancellationToken);

            foreach (var entity in due)
            {
                await action(new ScheduledCommand
                {
                    CommandName = entity.CommandName,
                    Data = entity.Data,
                    ExecuteTime = entity.ExecuteTime.Ticks,
                });

                db.ScheduledCommands.Remove(entity);
            }

            await db.SaveChangesAsync(cancellationToken);
        });

        scope.Complete();
    }

    // === Entity mapping ===

    private static WorkflowInstanceEntity ToEntity(WorkflowInstance workflow) => new()
    {
        Id = workflow.Id,
        WorkflowDefinitionId = workflow.WorkflowDefinitionId,
        Version = workflow.Version,
        Status = (int)workflow.Status,
        Description = workflow.Description,
        Reference = workflow.Reference,
        CreateTime = workflow.CreateTime,
        NextExecution = workflow.NextExecution,
        CompleteTime = workflow.CompleteTime,
        Data = JsonConvert.SerializeObject(workflow, JsonSettings),
    };

    private static void UpdateEntity(WorkflowInstanceEntity entity, WorkflowInstance workflow)
    {
        entity.Status = (int)workflow.Status;
        entity.Description = workflow.Description;
        entity.NextExecution = workflow.NextExecution;
        entity.CompleteTime = workflow.CompleteTime;
        entity.Data = JsonConvert.SerializeObject(workflow, JsonSettings);
    }

    private static WorkflowInstance ToDomain(WorkflowInstanceEntity entity) =>
        JsonConvert.DeserializeObject<WorkflowInstance>(entity.Data, JsonSettings)!;

    private static EventSubscriptionEntity ToEntity(EventSubscription sub) => new()
    {
        Id = sub.Id,
        WorkflowId = sub.WorkflowId,
        StepId = sub.StepId,
        ExecutionPointerId = sub.ExecutionPointerId,
        EventName = sub.EventName,
        EventKey = sub.EventKey,
        SubscribeAsOf = sub.SubscribeAsOf,
        SubscriptionData = sub.SubscriptionData is not null
            ? JsonConvert.SerializeObject(sub.SubscriptionData, JsonSettings)
            : null,
        ExternalToken = sub.ExternalToken,
        ExternalWorkerId = sub.ExternalWorkerId,
        ExternalTokenExpiry = sub.ExternalTokenExpiry,
    };

    private static EventSubscription ToDomain(EventSubscriptionEntity entity) => new()
    {
        Id = entity.Id,
        WorkflowId = entity.WorkflowId,
        StepId = entity.StepId,
        ExecutionPointerId = entity.ExecutionPointerId,
        EventName = entity.EventName,
        EventKey = entity.EventKey,
        SubscribeAsOf = entity.SubscribeAsOf,
        SubscriptionData = entity.SubscriptionData is not null
            ? JsonConvert.DeserializeObject(entity.SubscriptionData, JsonSettings)
            : null,
        ExternalToken = entity.ExternalToken,
        ExternalWorkerId = entity.ExternalWorkerId,
        ExternalTokenExpiry = entity.ExternalTokenExpiry,
    };

    private static EventEntity ToEntity(Event evt) => new()
    {
        Id = evt.Id,
        EventName = evt.EventName,
        EventKey = evt.EventKey,
        EventData = evt.EventData is not null
            ? JsonConvert.SerializeObject(evt.EventData, JsonSettings)
            : null,
        EventTime = evt.EventTime,
        IsProcessed = evt.IsProcessed,
    };

    private static Event ToDomain(EventEntity entity) => new()
    {
        Id = entity.Id,
        EventName = entity.EventName,
        EventKey = entity.EventKey,
        EventData = entity.EventData is not null
            ? JsonConvert.DeserializeObject(entity.EventData, JsonSettings)
            : null,
        EventTime = entity.EventTime,
        IsProcessed = entity.IsProcessed,
    };
}
