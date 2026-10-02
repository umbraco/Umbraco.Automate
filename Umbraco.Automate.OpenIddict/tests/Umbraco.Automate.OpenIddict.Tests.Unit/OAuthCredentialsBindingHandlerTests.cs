using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class OAuthCredentialsBindingHandlerTests
{
    private const string Key = "oAuthCredentialsId";

    private readonly Mock<IOAuthCredentialsService> _credentialsService = new();
    private readonly AdjustableTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly OAuthCredentialsHandoffProtector _protector;
    private readonly OAuthCredentialsBindingHandler _handler;
    private readonly TestOAuthConnectionType _connectionType;
    private readonly List<Connection> _persistedConnections = [];

    public OAuthCredentialsBindingHandlerTests()
    {
        _protector = new OAuthCredentialsHandoffProtector(new EphemeralDataProtectionProvider(), _time);
        _handler = new OAuthCredentialsBindingHandler(_protector, _credentialsService.Object);
        _connectionType = new TestOAuthConnectionType(_credentialsService.Object);
    }

    [Fact]
    public async Task AcceptsUnchangedCredentialId()
    {
        var existingId = Guid.NewGuid();
        var connection = NewConnection(existingId.ToString());

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: existingId), CancellationToken.None);

        StoredValue(connection).ShouldBe(existingId.ToString());
    }

    [Fact]
    public async Task AcceptsUnchangedCredentialId_SubmittedAsJsonElement()
    {
        var existingId = Guid.NewGuid();
        var connection = NewConnection(JsonSerializer.SerializeToElement(existingId.ToString()));

        await Should.NotThrowAsync(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: existingId), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRawCredentialIdThatIsNotTheConnectionsCurrentOne()
    {
        var foreignId = SetUpCredential("TestProvider");
        var connection = NewConnection(foreignId.ToString());

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRawCredentialIdOnCreate()
    {
        var foreignId = SetUpCredential("TestProvider");
        var connection = NewConnection(foreignId.ToString());

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null, isNew: true), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRawCredentialIdOfAnotherConnection()
    {
        var foreignId = SetUpCredential("TestProvider");
        _persistedConnections.Add(NewConnection(foreignId.ToString()));
        var currentId = Guid.NewGuid();
        var connection = NewConnection(foreignId.ToString());

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: currentId), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsRawCredentialIdForAnotherProvider()
    {
        var foreignId = SetUpCredential("OtherProvider");
        var connection = NewConnection(foreignId.ToString());

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null, isNew: true), CancellationToken.None));
    }

    [Fact]
    public async Task UnknownRawCredentialId_OnUpdate_KeepsCurrentCredentialId()
    {
        var currentId = Guid.NewGuid();
        var connection = NewConnection(Guid.NewGuid().ToString());

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: currentId), CancellationToken.None);

        StoredValue(connection).ShouldBe(currentId.ToString());
    }

    [Fact]
    public async Task UnknownRawCredentialId_OnCreate_StoresNoCredential()
    {
        var connection = NewConnection(Guid.NewGuid().ToString());

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null, isNew: true), CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task UnknownRawCredentialId_WhenNoCredentialIsStored_StoresNoCredential()
    {
        var connection = NewConnection(Guid.NewGuid().ToString());

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null), CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task UnknownRawCredentialId_WhenStoredCredentialIsEmpty_StoresNoCredential()
    {
        var connection = NewConnection(Guid.NewGuid().ToString());

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: Guid.Empty), CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task EnvironmentTransfer_KeepsTargetsCredential_WhenSourceIdDoesNotExistHere()
    {
        // Mimics a deployment: the target connection is authenticated with credential X, and the incoming
        // settings (merged over the target's) carry the source environment's credential Y, unknown here.
        var targetCredentialId = SetUpCredential("TestProvider");
        var sourceCredentialId = Guid.NewGuid();
        var existing = new Connection
        {
            Id = Guid.NewGuid(),
            Alias = "slack",
            Name = "Slack",
            Type = "test-oauth",
            Settings = new() { [Key] = targetCredentialId.ToString(), ["channel"] = "general" },
        };
        _persistedConnections.Add(existing);

        var incoming = new Connection
        {
            Id = existing.Id,
            Alias = existing.Alias,
            Name = existing.Name,
            Type = existing.Type,
            Settings = new(existing.Settings) { [Key] = sourceCredentialId.ToString(), ["channel"] = "releases" },
        };

        await _handler.PrepareSettingsAsync(
            new ConnectionSettingsSaveContext(
                incoming,
                _connectionType,
                existing.Settings,
                isRollback: false,
                _ => Task.FromResult<IEnumerable<Connection>>(_persistedConnections)),
            CancellationToken.None);

        StoredValue(incoming).ShouldBe(targetCredentialId.ToString());
        incoming.Settings["channel"].ShouldBe("releases");
    }

    [Fact]
    public async Task ExchangesValidTokenForCredentialId()
    {
        var credentialId = SetUpCredential("TestProvider");
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: Guid.NewGuid()), CancellationToken.None);

        StoredValue(connection).ShouldBe(credentialId.ToString());
    }

    [Fact]
    public async Task ExchangesValidTokenOnCreate()
    {
        var credentialId = SetUpCredential("TestProvider");
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null, isNew: true), CancellationToken.None);

        StoredValue(connection).ShouldBe(credentialId.ToString());
    }

    [Fact]
    public async Task RejectsTokenForCredentialClaimedByAnotherConnection()
    {
        var credentialId = SetUpCredential("TestProvider");
        _persistedConnections.Add(NewConnection(credentialId.ToString()));
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));

        var ex = await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null), CancellationToken.None));

        ex.Message.ShouldContain("another connection");
    }

    [Fact]
    public async Task AcceptsTokenForCredentialAlreadyReferencedByThisConnectionOnly()
    {
        // The persisted copy of the connection being saved is in the list of all connections.
        var credentialId = SetUpCredential("TestProvider");
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));
        _persistedConnections.Add(new Connection
        {
            Id = connection.Id,
            Alias = "self",
            Name = "Self",
            Type = "test-oauth",
            Settings = new() { [Key] = credentialId.ToString() },
        });

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: credentialId), CancellationToken.None);

        StoredValue(connection).ShouldBe(credentialId.ToString());
    }

    [Fact]
    public async Task RejectsExpiredToken()
    {
        var credentialId = SetUpCredential("TestProvider");
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));
        _time.Advance(OAuthCredentialsHandoffProtector.Lifetime + TimeSpan.FromMinutes(1));

        var ex = await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: Guid.NewGuid()), CancellationToken.None));

        ex.Message.ShouldContain("expired");
    }

    [Fact]
    public async Task AcceptsExpiredToken_WhenItIsForTheConnectionsCurrentCredential()
    {
        // The editor still holds the token after an earlier save; re-saving must keep working.
        var credentialId = Guid.NewGuid();
        var connection = NewConnection(_protector.Protect(credentialId, "TestProvider"));
        _time.Advance(TimeSpan.FromHours(2));

        await _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: credentialId), CancellationToken.None);

        StoredValue(connection).ShouldBe(credentialId.ToString());
    }

    [Fact]
    public async Task RejectsTokenForAnotherProvider()
    {
        var credentialId = SetUpCredential("OtherProvider");
        var connection = NewConnection(_protector.Protect(credentialId, "OtherProvider"));

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsTokenWhenCredentialNoLongerExists()
    {
        var connection = NewConnection(_protector.Protect(Guid.NewGuid(), "TestProvider"));

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsGarbage()
    {
        var connection = NewConnection("not-a-token");

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: null), CancellationToken.None));
    }

    [Fact]
    public async Task RejectsDuplicateKeysDifferingOnlyByCase()
    {
        var existingId = Guid.NewGuid();
        var connection = NewConnection(existingId.ToString());
        connection.Settings["OAuthCredentialsId"] = Guid.NewGuid().ToString();

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: existingId), CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AcceptsNoCredential(string? value)
    {
        var connection = NewConnection(value);

        await Should.NotThrowAsync(() =>
            _handler.PrepareSettingsAsync(Context(connection, persistedCredentialId: Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Rollback_RestoresEarlierCredentialId_WhenItStillQualifies()
    {
        var earlierId = SetUpCredential("TestProvider");
        var connection = NewConnection(earlierId.ToString());

        await _handler.PrepareSettingsAsync(
            Context(connection, persistedCredentialId: Guid.NewGuid(), isRollback: true),
            CancellationToken.None);

        StoredValue(connection).ShouldBe(earlierId.ToString());
    }

    [Fact]
    public async Task Rollback_DropsCredentialIdNowUsedByAnotherConnection()
    {
        var earlierId = SetUpCredential("TestProvider");
        _persistedConnections.Add(NewConnection(earlierId.ToString()));
        var connection = NewConnection(earlierId.ToString());

        await _handler.PrepareSettingsAsync(
            Context(connection, persistedCredentialId: Guid.NewGuid(), isRollback: true),
            CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task Rollback_DropsCredentialIdThatNoLongerExists()
    {
        // E.g. deleted by cleanup after the connection moved to another credential.
        var connection = NewConnection(Guid.NewGuid().ToString());

        await _handler.PrepareSettingsAsync(
            Context(connection, persistedCredentialId: Guid.NewGuid(), isRollback: true),
            CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task Rollback_DropsCredentialIdForAnotherProvider()
    {
        // A snapshot from before these checks could hold any credential id.
        var otherProvidersId = SetUpCredential("OtherProvider");
        var connection = NewConnection(otherProvidersId.ToString());

        await _handler.PrepareSettingsAsync(
            Context(connection, persistedCredentialId: Guid.NewGuid(), isRollback: true),
            CancellationToken.None);

        StoredValue(connection).ShouldBeNull();
    }

    [Fact]
    public async Task Rollback_KeepsCurrentCredentialIdWithoutLookingItUp()
    {
        var currentId = Guid.NewGuid();
        var connection = NewConnection(currentId.ToString());

        await _handler.PrepareSettingsAsync(
            Context(connection, persistedCredentialId: currentId, isRollback: true),
            CancellationToken.None);

        StoredValue(connection).ShouldBe(currentId.ToString());
        _credentialsService.Verify(
            s => s.GetCredentialsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Rollback_StillRejectsTokensThatFailValidation()
    {
        var connection = NewConnection("not-a-token");

        await Should.ThrowAsync<ConnectionSettingsValidationException>(() =>
            _handler.PrepareSettingsAsync(
                Context(connection, persistedCredentialId: Guid.NewGuid(), isRollback: true),
                CancellationToken.None));
    }

    [Fact]
    public async Task IgnoresNonOAuthConnectionTypes()
    {
        var connection = NewConnection(Guid.NewGuid().ToString());
        var context = new ConnectionSettingsSaveContext(
            connection,
            Mock.Of<IConnectionType>(),
            persistedSettings: null,
            isRollback: false,
            _ => Task.FromResult<IEnumerable<Connection>>(_persistedConnections));

        await Should.NotThrowAsync(() => _handler.PrepareSettingsAsync(context, CancellationToken.None));
    }

    private Guid SetUpCredential(string provider)
    {
        var id = Guid.NewGuid();
        _credentialsService
            .Setup(s => s.GetCredentialsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OAuthCredentials { Id = id, Provider = provider, AccessToken = "token" });
        return id;
    }

    private static Connection NewConnection(object? credentialValue) => new()
    {
        Id = Guid.NewGuid(),
        Alias = "conn",
        Name = "Conn",
        Type = "test-oauth",
        Settings = new() { [Key] = credentialValue },
    };

    private static string? StoredValue(Connection connection)
        => OAuthCredentialReferences.ReadString(connection.Settings[Key]);

    private ConnectionSettingsSaveContext Context(
        Connection connection,
        Guid? persistedCredentialId,
        bool isRollback = false,
        bool isNew = false)
    {
        Dictionary<string, object?>? persisted = isNew
            ? null
            : new() { [Key] = persistedCredentialId?.ToString() };

        return new ConnectionSettingsSaveContext(
            connection,
            _connectionType,
            persisted,
            isRollback,
            _ => Task.FromResult<IEnumerable<Connection>>(_persistedConnections));
    }

    private sealed class TestSettings
    {
        public Guid? OAuthCredentialsId { get; set; }
    }

    [ConnectionType("test-oauth", "Test OAuth")]
    private sealed class TestOAuthConnectionType : OAuthConnectionTypeBase<TestSettings>
    {
        public TestOAuthConnectionType(IOAuthCredentialsService credentialsService)
            : base(new ConnectionTypeInfrastructure(Mock.Of<IEditableModelResolver>()), credentialsService)
        {
        }

        public override string ProviderName => "TestProvider";
    }
}
