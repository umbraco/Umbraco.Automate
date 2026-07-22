using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Testing.Builders;

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
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    private static AutomationFactory CreatePassthroughFactory()
    {
        // Create a factory with a passthrough serializer (no encryption).
        var serializer = new EditableModelSerializer(
            Mock.Of<Core.Security.ISensitiveFieldProtector>(p =>
                p.IsProtected(It.IsAny<string>()) == false),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    [Fact]
    public void BuildEntity_AndBuildDomain_RoundTrips()
    {
        var factory = CreatePassthroughFactory();

        var stepId = Guid.NewGuid();
        var sourceStepId = Guid.NewGuid();
        var targetStepId = Guid.NewGuid();

        var automation = new AutomationBuilder()
            .WithAlias("testAutomation")
            .WithName("Test Automation")
            .WithDescription("A test")
            .WithVersion(5)
            .WithPublishedVersion(2)
            .WithDateCreated(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .WithDateModified(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))
            .WithCreatedByUserId(Guid.NewGuid())
            .WithModifiedByUserId(Guid.NewGuid())
            .WithTrigger("umbracoAutomate.contentPublished", new Dictionary<string, object?> { ["contentTypeAlias"] = "blogPost" })
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.logMessage")
                .WithName("Log it")
                .WithSetting("message", "hello"))
            .WithConnection(sourceStepId, targetStepId, "true")
            .WithCanvasState("{\"zoom\":1}")
            .Build();

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
    public void BuildEntity_AndBuildDomain_RoundTrips_StepAlias()
    {
        var factory = CreatePassthroughFactory();

        var automation = new AutomationBuilder()
            .WithAlias("aliasTest")
            .WithName("Alias Test")
            .AddStep(new StepConfigurationBuilder()
                .WithActionAlias("umbracoAutomate.httpRequest")
                .WithName("Fetch Data")
                .WithAlias("httpRequest"))
            .Build();

        AutomationEntity entity = factory.BuildEntity(automation);
        Automation roundTripped = factory.BuildDomain(entity);

        roundTripped.Steps.Count.ShouldBe(1);
        roundTripped.Steps[0].Alias.ShouldBe("httpRequest");
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
    public void BuildEntity_EncryptsNestedWebhookAuthenticatorSecret()
    {
        // Top-level Secret field used to be on WebhookTriggerSettings, but sensitive fields
        // now live on per-strategy settings (e.g. PlainSecretWebhookAuthenticatorSettings.Secret).
        // The factory must encrypt those nested fields using the selected authenticator's schema.
        var protector = new Mock<ISensitiveFieldProtector>();
        protector.Setup(p => p.Protect(It.IsAny<string>())).Returns((string v) => $"ENC:{v}");
        protector.Setup(p => p.IsProtected(It.IsAny<string>()))
            .Returns((string v) => v?.StartsWith("ENC:", StringComparison.Ordinal) == true);
        protector.Setup(p => p.Unprotect(It.IsAny<string>()))
            .Returns((string v) => v[4..]);

        var serializer = new EditableModelSerializer(protector.Object, new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
        var triggers = new TriggerCollection(() =>
        {
            var deps = new TriggerInfrastructure(modelResolver);
            return new ITrigger[] { new Core.Triggers.BuiltIn.WebhookTrigger(deps) };
        });
        var authenticators = new WebhookAuthenticatorCollection(() =>
            new IWebhookAuthenticator[]
            {
                new PlainSecretWebhookAuthenticator(),
                new HmacSha256WebhookAuthenticator(),
            });

        var factory = new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            triggers,
            authenticators);

        var automation = new AutomationBuilder()
            .WithAlias("webhookTest")
            .WithName("Webhook Test")
            .WithWebhookTrigger(
                PlainSecretWebhookAuthenticator.WellKnownAlias,
                new PlainSecretWebhookAuthenticatorSettings { Secret = "raw-plaintext-secret" })
            .Build();

        var entity = factory.BuildEntity(automation);

        entity.Definition.ShouldNotBeNull();
        entity.Definition.ShouldContain("ENC:raw-plaintext-secret");
        entity.Definition.ShouldNotContain("\"raw-plaintext-secret\"");

        // Round-trip restores plaintext via the serializer's recursive decrypt.
        var roundTripped = factory.BuildDomain(entity);
        roundTripped.Trigger.ShouldNotBeNull();
        var authElement = (System.Text.Json.JsonElement)roundTripped.Trigger.Settings["authenticator"]!;
        authElement.GetProperty("settings").GetProperty("secret").GetString()
            .ShouldBe("raw-plaintext-secret");
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

        Automation updated = new AutomationBuilder()
            .WithAlias("updated")
            .WithName("Updated")
            .WithVersion(2)
            .WithModifiedByUserId(Guid.NewGuid());

        factory.UpdateEntity(entity, updated);

        entity.Alias.ShouldBe("updated");
        entity.Name.ShouldBe("Updated");
        entity.DateCreated.ShouldBe(originalCreated);
        entity.CreatedByUserId.ShouldBe(originalCreatedBy);
    }
}
