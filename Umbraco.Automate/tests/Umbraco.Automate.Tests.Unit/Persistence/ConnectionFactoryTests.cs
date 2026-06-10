using Moq;
using Shouldly;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Persistence.Connections;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class ConnectionFactoryTests
{
    private readonly Mock<IEditableModelSerializer> _serializer = new();
    private readonly ConnectionTypeCollection _connectionTypes;
    private readonly ConnectionFactory _factory;

    public ConnectionFactoryTests()
    {
        _connectionTypes = new ConnectionTypeCollection(() => []);

        // Default serializer: pass-through (no encryption/decryption)
        _serializer.Setup(s => s.Serialize(It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns((object? model, EditableModelSchema? _) =>
                model is null ? null : System.Text.Json.JsonSerializer.Serialize(model));
        _serializer.Setup(s => s.Deserialize<Dictionary<string, object?>>(It.IsAny<string?>()))
            .Returns((string? json) =>
                string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? []);

        _factory = new ConnectionFactory(_serializer.Object, _connectionTypes);
    }

    [Fact]
    public void BuildEntity_AndBuildDomain_RoundTrips()
    {
        var connection = new Connection
        {
            Id = Guid.NewGuid(),
            Alias = "testConnection",
            Name = "Test Connection",
            Type = "slack",
            Settings = new Dictionary<string, object?> { ["webhookUrl"] = "https://hooks.slack.com/test" },
            Version = 3,
            DateCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DateModified = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = Guid.NewGuid(),
            ModifiedByUserId = Guid.NewGuid(),
        };

        ConnectionEntity entity = _factory.BuildEntity(connection);
        Connection roundTripped = _factory.BuildDomain(entity);

        roundTripped.Id.ShouldBe(connection.Id);
        roundTripped.Alias.ShouldBe(connection.Alias);
        roundTripped.Name.ShouldBe(connection.Name);
        roundTripped.Type.ShouldBe(connection.Type);
        roundTripped.Version.ShouldBe(3);
        roundTripped.DateCreated.ShouldBe(connection.DateCreated);
        roundTripped.DateModified.ShouldBe(connection.DateModified);
        roundTripped.CreatedByUserId.ShouldBe(connection.CreatedByUserId);
        roundTripped.ModifiedByUserId.ShouldBe(connection.ModifiedByUserId);
    }

    [Fact]
    public void BuildDomain_EmptySettings_ReturnsEmptyDictionary()
    {
        var entity = new ConnectionEntity
        {
            Id = Guid.NewGuid(),
            Alias = "empty",
            Name = "Empty",
            Type = "slack",
            Settings = null,
            Version = 1,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        };

        var connection = _factory.BuildDomain(entity);

        connection.Settings.ShouldBeEmpty();
    }

    [Fact]
    public void BuildEntity_EmptySettings_SetsNullOnEntity()
    {
        var connection = new Connection
        {
            Alias = "noSettings",
            Name = "No Settings",
            Type = "slack",
            Settings = [],
        };

        var entity = _factory.BuildEntity(connection);

        entity.Settings.ShouldBeNull();
    }

    [Fact]
    public void UpdateEntity_DoesNotModifyCreatedFields()
    {
        var originalCreated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var originalCreatedBy = Guid.NewGuid();

        var entity = new ConnectionEntity
        {
            Id = Guid.NewGuid(),
            Alias = "original",
            Name = "Original",
            Type = "slack",
            Version = 1,
            DateCreated = originalCreated,
            DateModified = originalCreated,
            CreatedByUserId = originalCreatedBy,
        };

        var updated = new Connection
        {
            Alias = "updated",
            Name = "Updated",
            Type = "smtp",
            Settings = new Dictionary<string, object?> { ["host"] = "smtp.example.com" },
            Version = 2,
            DateModified = DateTime.UtcNow,
            ModifiedByUserId = Guid.NewGuid(),
        };

        _factory.UpdateEntity(entity, updated);

        entity.Alias.ShouldBe("updated");
        entity.Name.ShouldBe("Updated");
        entity.Type.ShouldBe("smtp");
        entity.DateCreated.ShouldBe(originalCreated);
        entity.CreatedByUserId.ShouldBe(originalCreatedBy);
    }

    [Fact]
    public void SerializeSettings_DelegatesToEditableModelSerializer()
    {
        var connection = new Connection
        {
            Alias = "test",
            Name = "Test",
            Type = "slack",
            Settings = new Dictionary<string, object?> { ["apiKey"] = "secret123" },
        };

        _factory.BuildEntity(connection);

        _serializer.Verify(s => s.Serialize(
            It.Is<object>(o => o is Dictionary<string, object?>),
            It.IsAny<EditableModelSchema?>()), Times.Once);
    }
}
