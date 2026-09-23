using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Versioning;
using Umbraco.Automate.Tests.Common.Fixtures;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="SensitiveSettingsReprotectionJob"/> against in-memory SQLite:
/// plaintext secrets left by earlier versions are encrypted in place, once.
/// </summary>
public class SensitiveSettingsReprotectionJobTests : IDisposable
{
    private const string ActionAlias = "test.sendRequest";
    private const string ChannelAlias = "test.webhookChannel";

    private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly EfCoreTestFixture _fixture = new();
    private readonly Mock<IKeyValueService> _keyValueService = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task PerformExecuteAsync_EncryptsPlaintextSecrets_AndRecordsCompletion()
    {
        var automationId = Guid.NewGuid();
        var definition = JsonSerializer.Serialize(
            new AutomationDefinitionDto
            {
                Steps = [Step("ENC:already")],
                NotificationSettings = Channels("channel-plain"),
            },
            CamelCase);
        var automationSnapshot = JsonSerializer.Serialize(
            new Automation { Id = automationId, Alias = "a", Name = "A", Steps = [Step("step-plain")] },
            AutomationVersionableEntityAdapter.SerializerOptions);
        const string connectionSnapshot = """{"settings":"left-alone"}""";

        await SeedAsync(automationId, definition, automationSnapshot, connectionSnapshot);

        await CreateJob().PerformExecuteAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var storedDefinition = db.Automations.Single().Definition;
        storedDefinition.ShouldNotBeNull();
        storedDefinition.ShouldContain("ENC:channel-plain");
        storedDefinition.ShouldContain("ENC:already");
        storedDefinition.ShouldNotContain("ENC:ENC:");

        var versions = db.EntityVersions.ToList();
        versions.Single(v => v.EntityType == nameof(Automation)).Snapshot.ShouldContain("ENC:step-plain");
        versions.Single(v => v.EntityType == "Connection").Snapshot.ShouldBe(connectionSnapshot);

        _keyValueService.Verify(k => k.SetValue(SensitiveSettingsReprotectionJob.CompletedKey, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PerformExecuteAsync_WhenAlreadyCompleted_ChangesNothing()
    {
        var automationId = Guid.NewGuid();
        var definition = JsonSerializer.Serialize(
            new AutomationDefinitionDto { Steps = [Step("step-plain")], NotificationSettings = Channels("channel-plain") },
            CamelCase);
        await SeedAsync(automationId, definition, automationSnapshot: null, connectionSnapshot: null);
        _keyValueService.Setup(k => k.GetValue(SensitiveSettingsReprotectionJob.CompletedKey)).Returns("2026-01-01T00:00:00Z");

        await CreateJob().PerformExecuteAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        db.Automations.Single().Definition.ShouldBe(definition);
        _keyValueService.Verify(k => k.SetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task PerformExecuteAsync_OnSubscriberServer_DoesNotRun()
    {
        var definition = JsonSerializer.Serialize(
            new AutomationDefinitionDto { Steps = [Step("step-plain")] },
            CamelCase);
        await SeedAsync(Guid.NewGuid(), definition, automationSnapshot: null, connectionSnapshot: null);

        await CreateJob(ServerRole.Subscriber).PerformExecuteAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        db.Automations.Single().Definition.ShouldBe(definition);
        _keyValueService.Verify(k => k.GetValue(It.IsAny<string>()), Times.Never);
    }

    private async Task SeedAsync(Guid automationId, string definition, string? automationSnapshot, string? connectionSnapshot)
    {
        await using var db = _fixture.CreateContext();

        db.Automations.Add(new AutomationEntity
        {
            Id = automationId,
            Alias = "withSecrets",
            Name = "With secrets",
            Definition = definition,
            Version = 1,
            DateCreated = DateTime.UtcNow,
            DateModified = DateTime.UtcNow,
        });

        if (automationSnapshot is not null)
        {
            db.EntityVersions.Add(new EntityVersionEntity
            {
                Id = Guid.NewGuid(),
                EntityId = automationId,
                EntityType = nameof(Automation),
                Version = 1,
                Snapshot = automationSnapshot,
                DateCreated = DateTime.UtcNow,
            });
        }

        if (connectionSnapshot is not null)
        {
            db.EntityVersions.Add(new EntityVersionEntity
            {
                Id = Guid.NewGuid(),
                EntityId = Guid.NewGuid(),
                EntityType = "Connection",
                Version = 1,
                Snapshot = connectionSnapshot,
                DateCreated = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    private SensitiveSettingsReprotectionJob CreateJob(ServerRole role = ServerRole.Single)
    {
        var serializer = new EditableModelSerializer(
            CreateFieldProtector(),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        var action = new Mock<IAction>();
        action.SetupGet(a => a.Alias).Returns(ActionAlias);
        action.Setup(a => a.GetSettingsSchema()).Returns(Schema("headers"));

        var channel = new Mock<INotificationChannel>();
        channel.SetupGet(c => c.Alias).Returns(ChannelAlias);
        channel.Setup(c => c.GetSettingsSchema()).Returns(Schema("secret"));

        var settingsProtector = new AutomationSettingsProtector(
            serializer,
            new ActionCollection(() => [action.Object]),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>),
            new NotificationChannelCollection(() => [channel.Object]));

        var readiness = new AutomateReadinessSignal();
        readiness.Signal();

        return new SensitiveSettingsReprotectionJob(
            new TestDbContextFactory(_fixture.CreateContext),
            new AutomationFactory(serializer, settingsProtector),
            settingsProtector,
            _keyValueService.Object,
            readiness,
            Mock.Of<IRuntimeState>(r => r.Level == RuntimeLevel.Run),
            Mock.Of<IServerRoleAccessor>(s => s.CurrentServerRole == role),
            Mock.Of<IMainDom>(m => m.IsMainDom == true),
            NullLogger<SensitiveSettingsReprotectionJob>.Instance);
    }

    private static ISensitiveFieldProtector CreateFieldProtector()
    {
        var protector = new Mock<ISensitiveFieldProtector>();
        protector.Setup(p => p.IsProtected(It.IsAny<string>()))
            .Returns((string? v) => v?.StartsWith("ENC:", StringComparison.Ordinal) == true);
        protector.Setup(p => p.Protect(It.IsAny<string>()))
            .Returns((string? v) => string.IsNullOrEmpty(v) || v.StartsWith("ENC:", StringComparison.Ordinal) ? v : $"ENC:{v}");
        protector.Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns((string? v) => v is not null && v.StartsWith("ENC:", StringComparison.Ordinal) ? v[4..] : v);
        return protector.Object;
    }

    private static EditableModelSchema Schema(string sensitiveKey) => new()
    {
        Fields =
        [
            new EditableModelFieldDescriptor
            {
                Key = sensitiveKey,
                Label = sensitiveKey,
                PropertyName = sensitiveKey,
                PropertyType = typeof(string),
                IsSensitive = true,
            },
        ],
    };

    private static StepConfiguration Step(string secret) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = ActionAlias,
        Name = "Send request",
        Settings = new Dictionary<string, object?> { ["headers"] = secret },
    };

    private static AutomationNotificationSettings Channels(string secret) => new()
    {
        Channels =
        [
            new ChannelConfiguration
            {
                ChannelAlias = ChannelAlias,
                Settings = new Dictionary<string, object?> { ["secret"] = secret },
            },
        ],
    };
}
