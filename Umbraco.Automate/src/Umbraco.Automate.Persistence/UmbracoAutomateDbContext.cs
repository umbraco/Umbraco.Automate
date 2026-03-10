using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Runs;

namespace Umbraco.Automate.Persistence;

/// <summary>
/// EF Core database context for Umbraco Automate entities.
/// </summary>
public class UmbracoAutomateDbContext : DbContext
{
    internal DbSet<AutomationEntity> Automations { get; set; } = null!;

    internal DbSet<AutomationRunEntity> AutomationRuns { get; set; } = null!;

    internal DbSet<StepRunEntity> StepRuns { get; set; } = null!;

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
    }
}
