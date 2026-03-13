using Microsoft.EntityFrameworkCore;
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

    internal DbSet<AutomationRunEntity> AutomationRuns { get; set; } = null!;

    internal DbSet<StepRunEntity> StepRuns { get; set; } = null!;

    internal DbSet<WorkflowInstanceEntity> WorkflowInstances { get; set; } = null!;

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
            entity.Property(e => e.IsEnabled).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Definition);
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Alias).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.GroupId);
            entity.HasIndex(e => e.WorkspaceId);
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
            entity.Property(e => e.Data).IsRequired();

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextExecution);
            entity.HasIndex(e => new { e.Status, e.NextExecution });
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
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1);
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
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1);
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
    }
}
