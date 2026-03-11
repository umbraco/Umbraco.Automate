using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Persistence.Automations;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class AutomationFactoryTests
{
    private readonly AutomationFactory _factory;

    public AutomationFactoryTests()
    {
        var serializerMock = new Mock<IEditableModelSerializer>();
        serializerMock
            .Setup(s => s.Serialize(It.IsAny<object>(), It.IsAny<EditableModelSchema>()))
            .Returns((string?)null);
        serializerMock
            .Setup(s => s.Deserialize(It.IsAny<string>()))
            .Returns(default(System.Text.Json.JsonElement));

        _factory = new AutomationFactory(
            serializerMock.Object,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>));
    }

    private static AutomationFactory CreatePassthroughFactory()
    {
        // Create a factory with a passthrough serializer (no encryption).
        var serializer = new EditableModelSerializer(
            Mock.Of<Core.Security.ISensitiveFieldProtector>(p =>
                p.IsProtected(It.IsAny<string>()) == false));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>));
    }

    [Fact]
    public void BuildEntity_AndBuildDomain_RoundTrips()
    {
        var factory = CreatePassthroughFactory();

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

        AutomationEntity entity = factory.BuildEntity(automation);
        Automation roundTripped = factory.BuildDomain(entity);

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
        var factory = CreatePassthroughFactory();

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

        var automation = factory.BuildDomain(entity);

        automation.Trigger.ShouldBeNull();
        automation.Steps.ShouldBeEmpty();
        automation.Connections.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateEntity_DoesNotModifyCreatedFields()
    {
        var factory = CreatePassthroughFactory();

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

        factory.UpdateEntity(entity, updated);

        entity.Alias.ShouldBe("updated");
        entity.Name.ShouldBe("Updated");
        entity.DateCreated.ShouldBe(originalCreated);
        entity.CreatedByUserId.ShouldBe(originalCreatedBy);
    }
}
