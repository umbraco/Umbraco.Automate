using System.Text.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Connectors.ServiceConnectors;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Tests.Unit.Connectors.ServiceConnectors;

public class UmbracoAutomateConnectionServiceConnectorTests
{
    private readonly Mock<IConnectionService> _connectionServiceMock = new();
    private readonly Mock<UmbracoAutomateDeploySettingsAccessor> _settingsAccessorMock;
    private readonly List<IConnectionType> _registeredConnectionTypes = [];
    private readonly UmbracoAutomateConnectionServiceConnector _connector;

    public UmbracoAutomateConnectionServiceConnectorTests()
    {
        _settingsAccessorMock = new Mock<UmbracoAutomateDeploySettingsAccessor>(MockBehavior.Strict, null!);
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings());

        // Default: httpBasicAuth is registered on the target. Individual tests can clear
        // or replace this to simulate a missing provider package.
        _registeredConnectionTypes.Add(BuildConnectionTypeMock("httpBasicAuth"));

        var connectionTypeCollection = new ConnectionTypeCollection(() => _registeredConnectionTypes);

        _connector = new UmbracoAutomateConnectionServiceConnector(
            _connectionServiceMock.Object,
            connectionTypeCollection,
            _settingsAccessorMock.Object);
    }

    private static IConnectionType BuildConnectionTypeMock(string alias)
    {
        var mock = new Mock<IConnectionType>();
        mock.SetupGet(x => x.Alias).Returns(alias);
        return mock.Object;
    }

    private Connection BuildConnection(Dictionary<string, object?>? settings = null) => new()
    {
        Alias = "test-connection",
        Name = "Test Connection",
        Type = "httpBasicAuth",
        Settings = settings ?? [],
    };

    [Fact]
    public async Task GetArtifactAsync_WithConfigurationReference_PreservesValue()
    {
        var connection = BuildConnection(new Dictionary<string, object?>
        {
            ["ApiKey"] = "$MyService:ApiKey",
            ["Endpoint"] = "https://api.example.com",
        });
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Settings.ShouldNotBeNull();
        var settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(artifact.Settings.Value);
        settings.ShouldNotBeNull();
        settings.ShouldContainKey("Endpoint");
        // $ config refs pass through — IgnoreEncrypted only filters ENC: prefixes.
        settings.ShouldContainKey("ApiKey");
        settings["ApiKey"]!.ToString().ShouldBe("$MyService:ApiKey");
    }

    [Fact]
    public async Task GetArtifactAsync_WithEncryptedValue_FiltersWhenIgnoreEncryptedTrue()
    {
        var connection = BuildConnection(new Dictionary<string, object?>
        {
            ["ApiKey"] = "ENC:abc123encrypted",
            ["Endpoint"] = "https://api.example.com",
        });
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Settings.ShouldNotBeNull();
        var settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(artifact.Settings.Value);
        settings.ShouldNotBeNull();
        settings.ShouldContainKey("Endpoint");
        settings.ShouldNotContainKey("ApiKey");
    }

    [Fact]
    public async Task GetArtifactAsync_WithIgnoreEncryptedFalse_PreservesEncryptedValue()
    {
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings
        {
            Connections = new UmbracoAutomateDeployConnectionSettings { IgnoreEncrypted = false },
        });

        var connection = BuildConnection(new Dictionary<string, object?>
        {
            ["ApiKey"] = "ENC:abc123encrypted",
        });
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Settings.ShouldNotBeNull();
        var settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(artifact.Settings.Value);
        settings.ShouldNotBeNull();
        settings.ShouldContainKey("ApiKey");
    }

    [Fact]
    public async Task GetArtifactAsync_WithIgnoreSettingsList_FiltersNamedFields()
    {
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings
        {
            Connections = new UmbracoAutomateDeployConnectionSettings
            {
                IgnoreEncrypted = false,
                IgnoreSettings = ["ApiKey"],
            },
        });

        var connection = BuildConnection(new Dictionary<string, object?>
        {
            ["ApiKey"] = "plain-secret",
            ["Endpoint"] = "https://api.example.com",
        });
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Settings.ShouldNotBeNull();
        var settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(artifact.Settings.Value);
        settings.ShouldNotBeNull();
        settings.ShouldContainKey("Endpoint");
        settings.ShouldNotContainKey("ApiKey");
    }

    [Fact]
    public async Task GetArtifactAsync_WithIgnoreSettingsCaseInsensitive_FiltersField()
    {
        _settingsAccessorMock.Setup(x => x.Settings).Returns(new UmbracoAutomateDeploySettings
        {
            Connections = new UmbracoAutomateDeployConnectionSettings
            {
                IgnoreEncrypted = false,
                IgnoreSettings = ["apikey"],
            },
        });

        var connection = BuildConnection(new Dictionary<string, object?>
        {
            ["ApiKey"] = "plain-secret",
        });
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        // Only the filtered field was present, so Settings becomes null after filtering.
        artifact.Settings.ShouldBeNull();
    }

    [Fact]
    public async Task GetArtifactAsync_WithEmptySettings_ReturnsNullSettings()
    {
        var connection = BuildConnection();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Settings.ShouldBeNull();
    }

    [Fact]
    public async Task GetArtifactAsync_CopiesAliasNameAndType()
    {
        var connection = BuildConnection();
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, connection.Id);

        var artifact = await _connector.GetArtifactAsync(udi, connection);

        artifact.ShouldNotBeNull();
        artifact.Alias.ShouldBe("test-connection");
        artifact.Name.ShouldBe("Test Connection");
        artifact.Type.ShouldBe("httpBasicAuth");
    }

    [Fact]
    public async Task GetArtifactAsync_WithNullEntity_ReturnsNull()
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, Guid.NewGuid());

        var artifact = await _connector.GetArtifactAsync(udi, null);

        artifact.ShouldBeNull();
    }

    [Fact]
    public async Task GetEntityAsync_DelegatesToConnectionService()
    {
        var id = Guid.NewGuid();
        var connection = BuildConnection();
        _connectionServiceMock
            .Setup(x => x.GetConnectionAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await _connector.GetEntityAsync(id);

        result.ShouldBe(connection);
    }

    [Fact]
    public void GetEntityName_ReturnsConnectionName()
    {
        var connection = BuildConnection();

        _connector.GetEntityName(connection).ShouldBe("Test Connection");
    }

    [Fact]
    public void UdiEntityType_ReturnsConnectionUdiType()
    {
        _connector.UdiEntityType.ShouldBe(UmbracoAutomateDeployConstants.UdiEntityType.Connection);
    }

    [Fact]
    public async Task ProcessAsync_WithUnknownConnectionType_ThrowsWithActionableMessage()
    {
        _registeredConnectionTypes.Clear();

        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, Guid.NewGuid());
        var artifact = new AutomateConnectionArtifact(udi, new ArtifactDependencyCollection())
        {
            Alias = "slack-team",
            Name = "Slack Team",
            Type = "slack",
        };
        var state = ArtifactDeployState.Create<AutomateConnectionArtifact, Connection>(artifact, null, _connector, 2);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _connector.ProcessAsync(state, Mock.Of<IDeployContext>(), 2));

        ex.Message.ShouldContain("'slack'");
        ex.Message.ShouldContain("Slack Team");
        _connectionServiceMock.Verify(
            x => x.CreateConnectionAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _connectionServiceMock.Verify(
            x => x.UpdateConnectionAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithRegisteredConnectionType_CreatesConnection()
    {
        var udi = new GuidUdi(UmbracoAutomateDeployConstants.UdiEntityType.Connection, Guid.NewGuid());
        var artifact = new AutomateConnectionArtifact(udi, new ArtifactDependencyCollection())
        {
            Alias = "basic",
            Name = "Basic Auth",
            Type = "httpBasicAuth",
        };
        var state = ArtifactDeployState.Create<AutomateConnectionArtifact, Connection>(artifact, null, _connector, 2);
        _connectionServiceMock
            .Setup(x => x.CreateConnectionAsync(It.IsAny<Connection>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection c, Guid? _, CancellationToken _) => c);

        await _connector.ProcessAsync(state, Mock.Of<IDeployContext>(), 2);

        _connectionServiceMock.Verify(
            x => x.CreateConnectionAsync(It.Is<Connection>(c => c.Type == "httpBasicAuth"), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
