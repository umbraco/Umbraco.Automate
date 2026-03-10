using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Transport;
using Umbraco.Automate.Persistence.Workflows;

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

    internal DbSet<TransportMessageEntity> TransportMessages { get; set; } = null!;

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
            entity.Property(e => e.DraftVersion).IsRequired();
            entity.Property(e => e.Definition);
            entity.Property(e => e.Version).IsRequired().HasDefaultValue(1);
            entity.Property(e => e.DateCreated).IsRequired();
            entity.Property(e => e.DateModified).IsRequired();

            entity.HasIndex(e => e.Alias).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.GroupId);
        });

        modelBuilder.Entity<AutomationRunEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateRun");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AutomationId).IsRequired();
            entity.Property(e => e.AutomationVersion).IsRequired();
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

        // Database-backed CAP transport table

        modelBuilder.Entity<TransportMessageEntity>(entity =>
        {
            entity.ToTable("umbracoAutomateTransportMessage");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.Topic).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Headers).IsRequired();
            entity.Property(e => e.CreatedUtc).IsRequired();
            entity.Property(e => e.ClaimedByGroup).HasMaxLength(200);

            entity.HasIndex(e => new { e.Topic, e.ClaimedByGroup });
            entity.HasIndex(e => e.CreatedUtc);
        });
    }
}
