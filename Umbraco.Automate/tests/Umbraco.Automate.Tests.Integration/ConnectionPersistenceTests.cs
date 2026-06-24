using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Persistence.Connections;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreConnectionRepository"/> filtering against in-memory SQLite.
/// On SQLite, <c>string.Contains</c> translates to a case-sensitive <c>instr()</c> by default, so these
/// tests guard that name/alias filtering of the connection list is case-insensitive.
/// </summary>
public class ConnectionPersistenceTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreConnectionRepository _repository;

    public ConnectionPersistenceTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _repository = new EFCoreConnectionRepository(dbContextFactory, CreateFactory());
    }

    private static ConnectionFactory CreateFactory()
    {
        var serializer = new Mock<IEditableModelSerializer>();
        serializer
            .Setup(s => s.Serialize(It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns((object? model, EditableModelSchema? _) =>
                model is null ? null : System.Text.Json.JsonSerializer.Serialize(model));
        serializer
            .Setup(s => s.Deserialize<Dictionary<string, object?>>(It.IsAny<string?>()))
            .Returns((string? json) =>
                string.IsNullOrEmpty(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? []);

        return new ConnectionFactory(serializer.Object, new ConnectionTypeCollection(() => []));
    }

    [Theory]
    [InlineData("slack")]
    [InlineData("SLACK")]
    [InlineData("Slack")]
    [InlineData("sLaCk")]
    public async Task GetPaged_MatchesNameCaseInsensitively(string filter)
    {
        await _repository.SaveAsync(new ConnectionBuilder()
            .WithName("Slack Notifier")
            .WithAlias("distinct-alias-no-match")
            .Build());

        var (items, total) = await _repository.GetPagedAsync(filter);

        total.ShouldBe(1);
        items.Single().Name.ShouldBe("Slack Notifier");
    }

    [Theory]
    [InlineData("teamshook")]
    [InlineData("TEAMSHOOK")]
    [InlineData("TeamsHook")]
    public async Task GetPaged_MatchesAliasCaseInsensitively(string filter)
    {
        await _repository.SaveAsync(new ConnectionBuilder()
            .WithName("Distinct name no match")
            .WithAlias("TeamsHook")
            .Build());

        var (items, total) = await _repository.GetPagedAsync(filter);

        total.ShouldBe(1);
        items.Single().Alias.ShouldBe("TeamsHook");
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
