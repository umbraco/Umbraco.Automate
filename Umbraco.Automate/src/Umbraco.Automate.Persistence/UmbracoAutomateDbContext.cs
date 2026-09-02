using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Connections;
using Umbraco.Automate.Persistence.Outbox;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Versioning;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Persistence.Triggers;
using Umbraco.Automate.Persistence.Workspaces;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// EF Core database context for Umbraco Automate entities.
/// </summary>
public class UmbracoAutomateDbContext : DbContext
{
    internal DbSet<AutomationEntity> Automations { get; set; } = null!;

    internal DbSet<AutomationHealthEntity> AutomationHealth { get; set; } = null!;

    internal DbSet<AutomationRunEntity> AutomationRuns { get; set; } = null!;

    internal DbSet<StepRunEntity> StepRuns { get; set; } = null!;

    internal DbSet<WorkflowInstanceEntity> WorkflowInstances { get; set; } = null!;

    internal DbSet<WorkflowExecutionPointerEntity> WorkflowExecutionPointers { get; set; } = null!;

    internal DbSet<WorkflowLockEntity> WorkflowLocks { get; set; } = null!;

    internal DbSet<EventSubscriptionEntity> EventSubscriptions { get; set; } = null!;

    internal DbSet<EventEntity> Events { get; set; } = null!;

    internal DbSet<ScheduledCommandEntity> ScheduledCommands { get; set; } = null!;

    internal DbSet<OutboxMessageEntity> OutboxMessages { get; set; } = null!;

    internal DbSet<EntityVersionEntity> EntityVersions { get; set; } = null!;

    internal DbSet<WorkspaceEntity> Workspaces { get; set; } = null!;

    internal DbSet<ConnectionEntity> Connections { get; set; } = null!;

    internal DbSet<ScheduledTriggerStateEntity> ScheduledTriggerStates { get; set; } = null!;

    internal DbSet<WorkspaceGroupEntity> WorkspaceGroups { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoAutomateDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public UmbracoAutomateDbContext(DbContextOptions<UmbracoAutomateDbContext> options)
        : base(options)
    {
    }

    private static readonly AutomateMigrationsAssemblies MigrationsAssemblies = new(
        SqlServer: "Umbraco.Automate.Persistence.SqlServer",
        Sqlite: "Umbraco.Automate.Persistence.Sqlite");

    /// <summary>
    /// Configures the EF Core database provider with the correct migrations assembly.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
        => AutomateDbProvider.Configure(options, connectionString, providerName, MigrationsAssemblies);

    /// <summary>
    /// Configures the EF Core database provider against an already-open connection owned by someone
    /// else — the ambient Umbraco scope — so writes join that connection's transaction.
    /// </summary>
    internal static void ConfigureProvider(
        DbContextOptionsBuilder options,
        DbConnection connection,
        string providerName)
        => AutomateDbProvider.Configure(options, connection, providerName, MigrationsAssemblies);

    // All DateTime columns in this DbContext represent UTC instants. Neither SQL Server's
    // datetime2 nor SQLite's TEXT format preserves DateTimeKind across a round-trip, so
    // these converters reattach Kind=Utc on read (and normalize stray Local values on write)
    // for every DateTime/DateTime? property in the model. Without this, callers that rely
    // on the kind — Cronos, the API's UtcDateTimeJsonConverter, anything calling
    // ToUniversalTime — would silently shift values by the server's local offset.
    private static readonly ValueConverter<DateTime, DateTime> s_utcConverter = new(
        v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> s_utcNullableConverter = new(
        v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AutomationEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateAutomation");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Alias).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Definition);
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Alias).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.WorkspaceId);
        });

        modelBuilder.Entity<AutomationHealthEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateAutomationHealth");
            entity.HasKey(e => e.AutomationId);

            entity.Property(e => e.Health).IsRequired();

            entity.HasIndex(e => e.Health);
        });

        modelBuilder.Entity<AutomationRunEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateRun");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AutomationId).IsRequired();
            entity.Property(e => e.AutomationVersion).IsRequired();
            entity.Property(e => e.WorkspaceId).IsRequired();
            entity.Property(e => e.ServiceAccountKey).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.TriggerData);
            entity.Property(e => e.InitiatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(255);
            entity.Property(e => e.Error);
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(200);

            entity.HasIndex(e => e.AutomationId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.AutomationId, e.StartedUtc });
        });

        modelBuilder.Entity<StepRunEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateStepRun");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RunId).IsRequired();
            entity.Property(e => e.StepId).IsRequired();
            entity.Property(e => e.ActionAlias).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.InputData);
            entity.Property(e => e.OutputData);
            entity.Property(e => e.LogEntries);
            entity.Property(e => e.Error);
            entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.DurationTicks);

            entity.HasIndex(e => e.RunId);
            entity.HasIndex(e => new { e.RunId, e.StepId });
        });

        // WorkflowCore persistence tables

        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkflowInstance");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WorkflowDefinitionId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(200);
            entity.Property(e => e.CreateTime).IsRequired();
            entity.Property(e => e.SchemaVersion).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextExecution);
            entity.HasIndex(e => new { e.Status, e.NextExecution });

            entity.HasMany(e => e.ExecutionPointers)
                .WithOne()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowExecutionPointerEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkflowExecutionPointer");
            entity.HasKey(e => e.PersistenceId);

            entity.Property(e => e.PersistenceId).ValueGeneratedOnAdd();
            entity.Property(e => e.WorkflowInstanceId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PointerId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StepId).IsRequired();
            entity.Property(e => e.Active).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.PredecessorId).HasMaxLength(100);
            entity.Property(e => e.EventName).HasMaxLength(100);
            entity.Property(e => e.EventKey).HasMaxLength(100);
            entity.Property(e => e.EventPublished).IsRequired();
            entity.Property(e => e.StepName).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired();

            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => new { e.WorkflowInstanceId, e.Active });
            // Guards against a duplicate pointer row surviving even a lease-lock race (a slow
            // processing pass outliving its lease before renewal): without this, two rows for the
            // same pointer crash every future GetWorkflowInstance call for the instance
            // (ExecutionPointerCollection.Add's Dictionary.Add throws on the duplicate key).
            // EFCoreWorkflowPersistenceProvider catches the resulting DbUpdateException and
            // discards the losing write instead of letting it propagate.
            entity.HasIndex(e => new { e.WorkflowInstanceId, e.PointerId }).IsUnique();
        });

        modelBuilder.Entity<WorkflowLockEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkflowLock");
            entity.HasKey(e => e.LockId);

            entity.Property(e => e.LockId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OwnerToken).IsRequired();
            entity.Property(e => e.AcquiredUtc).IsRequired();
            entity.Property(e => e.ExpiresUtc).IsRequired();
        });

        modelBuilder.Entity<EventSubscriptionEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateEventSubscription");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(200).IsRequired();
            entity.Property(e => e.WorkflowId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ExecutionPointerId).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ExternalToken).HasMaxLength(200);
            entity.Property(e => e.ExternalWorkerId).HasMaxLength(200);

            entity.HasIndex(e => new { e.EventName, e.EventKey });
        });

        modelBuilder.Entity<EventEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateEvent");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.EventKey).HasMaxLength(200).IsRequired();

            entity.HasIndex(e => new { e.EventName, e.EventKey });
            entity.HasIndex(e => new { e.IsProcessed, e.EventTime });
        });

        modelBuilder.Entity<ScheduledCommandEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateScheduledCommand");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CommandName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.ExecuteTime).IsRequired();

            entity.HasIndex(e => e.ExecuteTime);
        });

        // Entity version table

        modelBuilder.Entity<EntityVersionEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateEntityVersion");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EntityId).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Version).IsRequired();
            entity.Property(e => e.Snapshot).IsRequired();
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.ChangeDescription).HasMaxLength(500);

            entity.HasIndex(e => new { e.EntityId, e.EntityType, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.EntityId, e.EntityType });
        });

        // Workspace tables

        modelBuilder.Entity<WorkspaceEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkspace");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Alias).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ServiceAccountKey).IsRequired();
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Alias).IsUnique();

            entity.HasMany(e => e.UserGroups)
                .WithOne()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AllowedConnections)
                .WithOne()
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceUserGroupEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkspaceUserGroup");
            entity.HasKey(e => new { e.WorkspaceId, e.UserGroupId });
        });

        modelBuilder.Entity<WorkspaceConnectionEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkspaceConnection");
            entity.HasKey(e => new { e.WorkspaceId, e.ConnectionId });
        });

        // Connection table

        modelBuilder.Entity<ConnectionEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateConnection");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Alias).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Settings);
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1).IsConcurrencyToken();
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Alias).IsUnique();
        });

        // Scheduled trigger state table

        modelBuilder.Entity<ScheduledTriggerStateEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateScheduledTriggerState");
            entity.HasKey(e => e.AutomationId);

            entity.Property(e => e.AutomationId).IsRequired();
            entity.Property(e => e.LastFiredUtc).IsRequired();
        });

        // Workspace group table

        modelBuilder.Entity<WorkspaceGroupEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateWorkspaceGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.DateCreated).IsRequired();

            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => e.ParentId);
        });

        // Outbox message table

        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateOutbox");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Topic).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Body).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(500);
            entity.Property(e => e.CreatedUtc).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RetryCount).IsRequired().HasDefaultValue(0);
            entity.Property(e => e.Error);
            entity.Property(e => e.ClaimedByInstance).HasMaxLength(200);
            entity.Property(e => e.ClaimedByInstance).IsConcurrencyToken();

            entity.HasIndex(e => new { e.Topic, e.Status, e.NextRetryUtc });
            entity.HasIndex(e => new { e.Status, e.CreatedUtc });
            // Non-unique index for idempotency lookups. Provider-specific migrations should add
            // a unique filtered index (WHERE IdempotencyKey IS NOT NULL) for safety.
            entity.HasIndex(e => new { e.Topic, e.IdempotencyKey });
        });

        ApplyUtcDateTimeConverters(modelBuilder);
    }

    private static void ApplyUtcDateTimeConverters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(s_utcConverter);
                }
                else if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(s_utcNullableConverter);
                }
            }
        }
    }
}
