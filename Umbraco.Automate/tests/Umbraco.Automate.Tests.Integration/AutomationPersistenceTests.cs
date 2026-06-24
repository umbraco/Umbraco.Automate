using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreAutomationRepository"/> filtering against in-memory SQLite.
/// On SQLite, <c>string.Contains</c> translates to a case-sensitive <c>instr()</c> by default, so these
/// tests guard that name/alias filtering (used by the collection list and global search) is case-insensitive.
/// </summary>
public class AutomationPersistenceTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreAutomationRepository _repository;

    public AutomationPersistenceTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _repository = new EFCoreAutomationRepository(dbContextFactory, CreateFactory());
    }

    private static AutomationFactory CreateFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false));

        return new AutomationFactory(
            serializer,
            new ActionCollection(Array.Empty<IAction>),
            new TriggerCollection(Array.Empty<ITrigger>),
            new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>));
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("MANUAL")]
    [InlineData("Manual")]
    [InlineData("mAnUaL")]
    public async Task GetPaged_MatchesNameCaseInsensitively(string filter)
    {
        await _repository.SaveAsync(new AutomationBuilder()
            .WithName("Manual")
            .WithAlias("distinct-alias-no-match")
            .Build());

        var (items, total) = await _repository.GetPagedAsync(filter);

        total.ShouldBe(1);
        items.Single().Name.ShouldBe("Manual");
    }

    [Theory]
    [InlineData("slacknotifier")]
    [InlineData("SLACKNOTIFIER")]
    [InlineData("SlackNotifier")]
    public async Task GetPaged_MatchesAliasCaseInsensitively(string filter)
    {
        await _repository.SaveAsync(new AutomationBuilder()
            .WithName("Distinct name no match")
            .WithAlias("SlackNotifier")
            .Build());

        var (items, total) = await _repository.GetPagedAsync(filter);

        total.ShouldBe(1);
        items.Single().Alias.ShouldBe("SlackNotifier");
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
