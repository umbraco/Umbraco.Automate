using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Umbraco.Automate.Core.Execution;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core-backed <see cref="IPersistenceProvider"/> for WorkflowCore.
/// Uses <see cref="IDbContextFactory{T}"/> for database access with an isolated connection.
/// </summary>
internal sealed class EFCoreWorkflowPersistenceProvider : IPersistenceProvider
{
    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly RunFinalizer _runFinalizer;
    private readonly ILogger<EFCoreWorkflowPersistenceProvider> _logger;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    // Storage format for the WorkflowInstance.Data column. Version 0 (legacy) stored the whole
    // serialized WorkflowInstance in Data with pointers inlined; version 1 stores only the
    // workflow Data payload and normalizes pointers into their own table. Rows are read via the
    // matching decoder and always rewritten as the current version on the next persist.
    private const int SchemaVersionNormalized = 1;

    public EFCoreWorkflowPersistenceProvider(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        RunFinalizer runFinalizer,
        ILogger<EFCoreWorkflowPersistenceProvider> logger)
    {
        _dbContextFactory = dbContextFactory;
        _runFinalizer = runFinalizer;
        _logger = logger;
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

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new WorkflowInstanceEntity
        {
            Id = workflow.Id,
            WorkflowDefinitionId = workflow.WorkflowDefinitionId,
            Data = string.Empty,
        };
        ApplyToEntity(entity, workflow);
        db.WorkflowInstances.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return workflow.Id;
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await LoadTrackedAsync(db, workflow.Id, cancellationToken);
        if (entity is not null)
        {
            try
            {
                ApplyToEntity(entity, workflow);
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(
                    ex,
                    "PersistWorkflow {Id}: A concurrent writer already committed a pointer update for this workflow instance. Discarding this pass's write instead of retrying.",
                    workflow.Id);
            }
        }

        await _runFinalizer.TryFinalizeAsync(workflow, cancellationToken);
    }

    public async Task PersistWorkflow(WorkflowInstance workflow, List<EventSubscription> subscriptions, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await LoadTrackedAsync(db, workflow.Id, cancellationToken);
        if (entity is not null)
        {
            ApplyToEntity(entity, workflow);
        }

        foreach (var sub in subscriptions)
        {
            if (string.IsNullOrEmpty(sub.Id))
            {
                sub.Id = Guid.NewGuid().ToString();
            }

            db.EventSubscriptions.Add(ToEntity(sub));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(
                ex,
                "PersistWorkflow {Id}: A concurrent writer already committed a pointer update for this workflow instance. Discarding this pass's write instead of retrying.",
                workflow.Id);
        }

        await _runFinalizer.TryFinalizeAsync(workflow, cancellationToken);
    }

    public async Task<WorkflowInstance> GetWorkflowInstance(string id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.WorkflowInstances
            .AsNoTracking()
            .Include(e => e.ExecutionPointers)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException($"Workflow instance '{id}' not found.");

        var wf = ToDomain(entity);
        _logger.LogDebug(
            "GetWorkflowInstance {Id}: Status={Status}, PointersNull={PointersNull}, PointerCount={Count}",
            id, wf.Status, wf.ExecutionPointers is null, wf.ExecutionPointers?.Count ?? -1);
        return wf;
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.WorkflowInstances
            .AsNoTracking()
            .Include(e => e.ExecutionPointers)
            .Where(e => idList.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IEnumerable<WorkflowInstance>> GetWorkflowInstances(
        WorkflowStatus? status, string type, DateTime? createdFrom, DateTime? createdTo, int skip, int take)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        IQueryable<WorkflowInstanceEntity> query = db.WorkflowInstances
            .AsNoTracking()
            .Include(e => e.ExecutionPointers);

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

        var entities = await query
            .OrderBy(e => e.CreateTime)
            .ThenBy(e => e.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
        return entities.Select(ToDomain).ToList();
    }

    public async Task<IEnumerable<string>> GetRunnableInstances(DateTime asAt, CancellationToken cancellationToken)
    {
        var ticks = asAt.Ticks;
        var runnableStatus = (int)WorkflowStatus.Runnable;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.WorkflowInstances
            .AsNoTracking()
            .Where(e => e.Status == runnableStatus && e.NextExecution.HasValue && e.NextExecution <= ticks)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    // === ISubscriptionRepository ===

    public async Task<string> CreateEventSubscription(EventSubscription subscription, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(subscription.Id))
        {
            subscription.Id = Guid.NewGuid().ToString();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        db.EventSubscriptions.Add(ToEntity(subscription));
        await db.SaveChangesAsync(cancellationToken);

        return subscription.Id;
    }

    public async Task<EventSubscription> GetSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EventSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventSubscriptionId, cancellationToken);

        return entity is not null ? ToDomain(entity) : throw new InvalidOperationException($"Subscription '{eventSubscriptionId}' not found.");
    }

    public async Task<EventSubscription> GetFirstOpenSubscription(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EventSubscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.EventName == eventName
                && e.EventKey == eventKey
                && e.SubscribeAsOf <= asOf
                && e.ExternalToken == null,
                cancellationToken);

        return entity is not null ? ToDomain(entity) : null!;
    }

    public async Task<IEnumerable<EventSubscription>> GetSubscriptions(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entities = await db.EventSubscriptions
            .AsNoTracking()
            .Where(e =>
                e.EventName == eventName
                && e.EventKey == eventKey
                && e.SubscribeAsOf <= asOf
                && e.ExternalToken == null)
            .ToListAsync(cancellationToken);

        var result = entities.Select(ToDomain).ToList();
        _logger.LogDebug(
            "GetSubscriptions: Name={Name}, Key={Key}, AsOf={AsOf}, Found={Count}",
            eventName, eventKey, asOf, result.Count);
        return result;
    }

    public async Task TerminateSubscription(string eventSubscriptionId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EventSubscriptions.FindAsync([eventSubscriptionId], cancellationToken);
        if (entity is not null)
        {
            db.EventSubscriptions.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> SetSubscriptionToken(
        string eventSubscriptionId, string token, string workerId, DateTime expiry, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
    }

    public async Task ClearSubscriptionToken(string eventSubscriptionId, string token, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.EventSubscriptions.FindAsync([eventSubscriptionId], cancellationToken);
        if (entity is not null && entity.ExternalToken == token)
        {
            entity.ExternalToken = null;
            entity.ExternalWorkerId = null;
            entity.ExternalTokenExpiry = null;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // === IEventRepository ===

    public async Task<string> CreateEvent(Event newEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(newEvent.Id))
        {
            newEvent.Id = Guid.NewGuid().ToString();
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        db.Events.Add(ToEntity(newEvent));
        await db.SaveChangesAsync(cancellationToken);

        return newEvent.Id;
    }

    public async Task<Event> GetEvent(string id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
            throw new InvalidOperationException($"Event '{id}' not found.");

        var evt = ToDomain(entity);
        _logger.LogDebug(
            "GetEvent {Id}: Name={Name}, Key={Key}, DataType={DataType}, DataNull={DataNull}, Processed={Processed}",
            id, evt.EventName, evt.EventKey, evt.EventData?.GetType().Name ?? "null", evt.EventData is null, evt.IsProcessed);
        return evt;
    }

    public async Task<IEnumerable<string>> GetRunnableEvents(DateTime asAt, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Events
            .AsNoTracking()
            .Where(e => !e.IsProcessed && e.EventTime <= asAt)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<string>> GetEvents(
        string eventName, string eventKey, DateTime asOf, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.Events
            .AsNoTracking()
            .Where(e => e.EventName == eventName && e.EventKey == eventKey && e.EventTime >= asOf)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkEventProcessed(string id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Events.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            entity.IsProcessed = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkEventUnprocessed(string id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await db.Events.FindAsync([id], cancellationToken);
        if (entity is not null)
        {
            entity.IsProcessed = false;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    // === IScheduledCommandRepository ===

    public bool SupportsScheduledCommands => true;

    public async Task ScheduleCommand(ScheduledCommand command)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        db.ScheduledCommands.Add(new ScheduledCommandEntity
        {
            CommandName = command.CommandName,
            Data = command.Data,
            ExecuteTime = new DateTime(command.ExecuteTime, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
    }

    public async Task ProcessCommands(
        DateTimeOffset asOf, Func<ScheduledCommand, Task> action, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

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
    }

    // === Entity mapping ===

    // Loads the instance with its pointer rows for change tracking, so SaveChanges writes only
    // the pointers that actually changed this pass (rather than rewriting the whole collection).
    private static Task<WorkflowInstanceEntity?> LoadTrackedAsync(
        UmbracoAutomateDbContext db, string id, CancellationToken cancellationToken) =>
        db.WorkflowInstances
            .Include(e => e.ExecutionPointers)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    // Maps a domain WorkflowInstance onto a (possibly tracked) entity: scalars, the workflow
    // Data payload, and an upsert of each pointer by PointerId. Always writes the normalized
    // format (SchemaVersion 1). Mirrors WorkflowCore's own EF provider ToPersistable(existing).
    private static void ApplyToEntity(WorkflowInstanceEntity entity, WorkflowInstance workflow)
    {
        entity.WorkflowDefinitionId = workflow.WorkflowDefinitionId;
        entity.Version = workflow.Version;
        entity.Status = (int)workflow.Status;
        entity.Description = workflow.Description;
        entity.Reference = workflow.Reference;
        entity.CreateTime = workflow.CreateTime;
        entity.NextExecution = workflow.NextExecution;
        entity.CompleteTime = workflow.CompleteTime;
        entity.SchemaVersion = SchemaVersionNormalized;
        entity.Data = JsonConvert.SerializeObject(workflow.Data, JsonSettings);

        foreach (var ep in workflow.ExecutionPointers)
        {
            var pe = entity.ExecutionPointers.FirstOrDefault(p => p.PointerId == ep.Id);
            if (pe is null)
            {
                pe = new WorkflowExecutionPointerEntity
                {
                    WorkflowInstanceId = entity.Id,
                    PointerId = ep.Id ?? Guid.NewGuid().ToString(),
                };
                entity.ExecutionPointers.Add(pe);
            }

            MapPointerToEntity(pe, ep);
        }
    }

    private static void MapPointerToEntity(WorkflowExecutionPointerEntity pe, ExecutionPointer ep)
    {
        pe.StepId = ep.StepId;
        pe.Active = ep.Active;
        pe.SleepUntil = ep.SleepUntil;
        pe.StartTime = ep.StartTime;
        pe.EndTime = ep.EndTime;
        pe.RetryCount = ep.RetryCount;
        pe.PredecessorId = ep.PredecessorId;
        pe.EventName = ep.EventName;
        pe.EventKey = ep.EventKey;
        pe.EventPublished = ep.EventPublished;
        pe.StepName = ep.StepName;
        pe.Status = (int)ep.Status;
        pe.Children = ep.Children.Count > 0 ? string.Join(';', ep.Children) : null;
        pe.Scope = ep.Scope.Count > 0 ? string.Join(';', ep.Scope) : null;
        pe.PersistenceData = SerializeField(ep.PersistenceData);
        pe.ContextItem = SerializeField(ep.ContextItem);
        pe.EventData = SerializeField(ep.EventData);
        pe.Outcome = SerializeField(ep.Outcome);
        pe.ExtensionAttributes = ep.ExtensionAttributes.Count > 0
            ? JsonConvert.SerializeObject(ep.ExtensionAttributes, JsonSettings)
            : null;
    }

    private static WorkflowInstance ToDomain(WorkflowInstanceEntity entity) =>
        entity.SchemaVersion == 0 ? LegacyToDomain(entity) : NormalizedToDomain(entity);

    // Legacy (SchemaVersion 0): the whole WorkflowInstance was serialized into Data with its
    // pointers inlined. Retained as a read-only fallback for rows written before pointer
    // normalization; they are rewritten as SchemaVersion 1 on their next persist. Removable in a
    // future major once no SchemaVersion 0 rows remain.
    private static WorkflowInstance LegacyToDomain(WorkflowInstanceEntity entity) =>
        JsonConvert.DeserializeObject<WorkflowInstance>(entity.Data, JsonSettings)!;

    private static WorkflowInstance NormalizedToDomain(WorkflowInstanceEntity entity)
    {
        var workflow = new WorkflowInstance
        {
            Id = entity.Id,
            WorkflowDefinitionId = entity.WorkflowDefinitionId,
            Version = entity.Version,
            Status = (WorkflowStatus)entity.Status,
            Description = entity.Description,
            Reference = entity.Reference,
            CreateTime = entity.CreateTime,
            NextExecution = entity.NextExecution,
            CompleteTime = entity.CompleteTime,
            Data = DeserializeField(entity.Data)!,
            ExecutionPointers = new ExecutionPointerCollection(entity.ExecutionPointers.Count + 8),
        };

        foreach (var pe in entity.ExecutionPointers)
        {
            workflow.ExecutionPointers.Add(MapPointerToDomain(pe));
        }

        return workflow;
    }

    private static ExecutionPointer MapPointerToDomain(WorkflowExecutionPointerEntity pe)
    {
        var ep = new ExecutionPointer
        {
            Id = pe.PointerId,
            StepId = pe.StepId,
            Active = pe.Active,
            SleepUntil = pe.SleepUntil,
            StartTime = pe.StartTime,
            EndTime = pe.EndTime,
            RetryCount = pe.RetryCount,
            PredecessorId = pe.PredecessorId,
            EventName = pe.EventName,
            EventKey = pe.EventKey,
            EventPublished = pe.EventPublished,
            StepName = pe.StepName,
            Status = (PointerStatus)pe.Status,
            PersistenceData = DeserializeField(pe.PersistenceData),
            ContextItem = DeserializeField(pe.ContextItem),
            EventData = DeserializeField(pe.EventData),
            Outcome = DeserializeField(pe.Outcome),
        };

        if (!string.IsNullOrEmpty(pe.Children))
        {
            ep.Children = pe.Children.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        if (!string.IsNullOrEmpty(pe.Scope))
        {
            ep.Scope = pe.Scope.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        if (!string.IsNullOrEmpty(pe.ExtensionAttributes))
        {
            var attributes = JsonConvert.DeserializeObject<Dictionary<string, object>>(pe.ExtensionAttributes, JsonSettings);
            if (attributes is not null)
            {
                ep.ExtensionAttributes = attributes;
            }
        }

        return ep;
    }

    private static string? SerializeField(object? value) =>
        value is null ? null : JsonConvert.SerializeObject(value, JsonSettings);

    private static object? DeserializeField(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject(json, JsonSettings);

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
