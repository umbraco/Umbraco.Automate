using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Persistence.Automations;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class AutomationFactoryTests
{
    [Fact]
    public void BuildEntity_AndBuildDomain_RoundTrips()
    {
        var automation = new Automation
        {
            Id = Guid.NewGuid(),
            Alias = "testAutomation",
            Name = "Test Automation",
            Description = "A test",
            IsEnabled = true,
            Status = AutomationStatus.Published,
            PublishedVersion = 2,
            DraftVersion = 3,
            Version = 5,
            DateCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateModified = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = Guid.NewGuid(),
            ModifiedByUserId = Guid.NewGuid(),
            Trigger = new TriggerConfiguration
            {
                TriggerAlias = "umbracoAutomate.contentPublished",
                Settings = new Dictionary<string, object?> { ["contentTypeAlias"] = "blogPost" },
            },
            Steps =
            [
                new StepConfiguration
                {
                    Id = Guid.NewGuid(),
                    ActionAlias = "umbracoAutomate.logMessage",
                    Name = "Log it",
                    Settings = new Dictionary<string, object?> { ["message"] = "hello" },
                },
            ],
            Connections =
            [
                new StepConnection
                {
                    SourceStepId = Guid.NewGuid(),
                    TargetStepId = Guid.NewGuid(),
                    Outcome = "true",
                },
            ],
            CanvasState = "{\"zoom\":1}",
        };

        AutomationEntity entity = AutomationFactory.BuildEntity(automation);
        Automation roundTripped = AutomationFactory.BuildDomain(entity);

        roundTripped.Id.ShouldBe(automation.Id);
        roundTripped.Alias.ShouldBe(automation.Alias);
        roundTripped.Name.ShouldBe(automation.Name);
        roundTripped.Status.ShouldBe(AutomationStatus.Published);
        roundTripped.Version.ShouldBe(5);
        roundTripped.Trigger.ShouldNotBeNull();
        roundTripped.Trigger.TriggerAlias.ShouldBe("umbracoAutomate.contentPublished");
        roundTripped.Steps.Count.ShouldBe(1);
        roundTripped.Steps[0].ActionAlias.ShouldBe("umbracoAutomate.logMessage");
        roundTripped.Connections.Count.ShouldBe(1);
        roundTripped.CanvasState.ShouldBe("{\"zoom\":1}");
    }

    [Fact]
    public void BuildDomain_NullDefinition_ReturnsEmptyCollections()
    {
        var entity = new AutomationEntity
        {
            Id = Guid.NewGuid(),
            Alias = "empty",
            Name = "Empty",
            Definition = null,
            Version = 1,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        var automation = AutomationFactory.BuildDomain(entity);

        automation.Trigger.ShouldBeNull();
        automation.Steps.ShouldBeEmpty();
        automation.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateEntity_DoesNotModifyCreatedFields()
    {
        var originalCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalCreatedBy = Guid.NewGuid();

        var entity = new AutomationEntity
        {
            Id = Guid.NewGuid(),
            Alias = "original",
            Name = "Original",
            Version = 1,
            DateCreated = originalCreated,
            DateModified = originalCreated,
            CreatedByUserId = originalCreatedBy,
        };

        var updated = new Automation
        {
            Alias = "updated",
            Name = "Updated",
            Version = 2,
            DateModified = DateTime.UtcNow,
            ModifiedByUserId = Guid.NewGuid(),
        };

        AutomationFactory.UpdateEntity(entity, updated);

        entity.Alias.ShouldBe("updated");
        entity.Name.ShouldBe("Updated");
        entity.DateCreated.ShouldBe(originalCreated);
        entity.CreatedByUserId.ShouldBe(originalCreatedBy);
    }
}
