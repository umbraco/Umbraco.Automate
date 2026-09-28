using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class OAuthCredentialsCleanupJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IOAuthCredentialsService> _credentialsService = new();
    private readonly Mock<IConnectionService> _connectionService = new();
    private readonly List<Connection> _connections = [];
    private readonly OAuthCredentialsCleanupJob _job;

    public OAuthCredentialsCleanupJobTests()
    {
        _connectionService
            .Setup(s => s.GetAllConnectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _connections);

        _job = new OAuthCredentialsCleanupJob(
            _credentialsService.Object,
            _connectionService.Object,
            new AdjustableTimeProvider(Now),
            NullLogger<OAuthCredentialsCleanupJob>.Instance);
    }

    [Fact]
    public void SelectUnreferenced_ExcludesIdsReferencedByAnyConnection()
    {
        var referenced = Guid.NewGuid();
        var referencedAsJson = Guid.NewGuid();
        var orphan = Guid.NewGuid();

        var connections = new[]
        {
            Connection(new() { ["oAuthCredentialsId"] = referenced.ToString() }),
            // Persisted settings deserialize to JsonElement.
            Connection(new() { ["oAuthCredentialsId"] = JsonSerializer.SerializeToElement(referencedAsJson.ToString()) }),
        };

        OAuthCredentialReferences.SelectUnreferenced([referenced, referencedAsJson, orphan], connections)
            .ShouldBe([orphan]);
    }

    [Fact]
    public void SelectUnreferenced_KeepsIdsReferencedUnderAnyKey()
    {
        // E.g. a connection whose type is no longer registered, so its schema is unknown.
        var id = Guid.NewGuid();

        OAuthCredentialReferences.SelectUnreferenced([id], [Connection(new() { ["somethingElse"] = id })])
            .ShouldBeEmpty();
    }

    [Fact]
    public void SelectUnreferenced_ReturnsAll_WhenNoConnections()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        OAuthCredentialReferences.SelectUnreferenced([a, b], []).ShouldBe([a, b]);
    }

    [Fact]
    public async Task DeleteUnreferenced_QueriesWith24HourCutoff_AndDeletesOnlyOrphans()
    {
        var referenced = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        _connections.Add(Connection(new() { ["oAuthCredentialsId"] = referenced.ToString() }));

        DateTime? cutoff = null;
        _credentialsService
            .Setup(s => s.GetCredentialIdsNotModifiedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((c, _) => cutoff = c)
            .ReturnsAsync([referenced, orphan]);

        var deleted = await _job.DeleteUnreferencedCredentialsAsync(CancellationToken.None);

        deleted.ShouldBe(1);
        cutoff.ShouldBe(Now.UtcDateTime.AddHours(-24));
        _credentialsService.Verify(s => s.DeleteCredentialsAsync(orphan, It.IsAny<CancellationToken>()), Times.Once);
        _credentialsService.Verify(s => s.DeleteCredentialsAsync(referenced, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteUnreferenced_SkipsConnectionLookup_WhenNothingIsStale()
    {
        _credentialsService
            .Setup(s => s.GetCredentialIdsNotModifiedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        (await _job.DeleteUnreferencedCredentialsAsync(CancellationToken.None)).ShouldBe(0);

        _connectionService.Verify(s => s.GetAllConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunJob_DoesNotThrow_WhenLookupFails()
    {
        _credentialsService
            .Setup(s => s.GetCredentialIdsNotModifiedSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("no table"));

        await Should.NotThrowAsync(() => _job.RunJobAsync(CancellationToken.None));
    }

    private static Connection Connection(Dictionary<string, object?> settings) => new()
    {
        Id = Guid.NewGuid(),
        Alias = "c",
        Name = "C",
        Type = "any",
        Settings = settings,
    };
}
