using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Round-trips <see cref="EFCoreAutomationHealthRepository"/> against in-memory SQLite, validating
/// the entity mapping, upsert behaviour, and that the <c>umbracoAutomateAutomationHealth</c> table
/// is created from the model.
/// </summary>
public class AutomationHealthRepositoryTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreAutomationHealthRepository _repo;

    public AutomationHealthRepositoryTests()
    {
        _fixture = new EfCoreTestFixture();
        _repo = new EFCoreAutomationHealthRepository(new TestDbContextFactory(_fixture.CreateContext));
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoRow()
    {
        var result = await _repo.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SaveAsync_Inserts_AndGetReturnsState_PreservingUtcKind()
    {
        var id = Guid.NewGuid();
        var disabledUtc = new DateTime(2026, 5, 20, 8, 0, 0, DateTimeKind.Utc);

        await _repo.SaveAsync(
            new AutomationHealthState
            {
                AutomationId = id,
                Health = AutomationHealth.Disabled,
                DisabledUtc = disabledUtc,
            },
            CancellationToken.None);

        var loaded = await _repo.GetAsync(id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded!.Health.ShouldBe(AutomationHealth.Disabled);
        loaded.DisabledUtc.ShouldNotBeNull();
        loaded.DisabledUtc!.Value.Kind.ShouldBe(DateTimeKind.Utc);
        loaded.DisabledUtc.Value.ShouldBe(disabledUtc);
    }

    [Fact]
    public async Task SaveAsync_Updates_ExistingRow()
    {
        var id = Guid.NewGuid();

        await _repo.SaveAsync(
            new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Degraded, WarningIssuedUtc = DateTime.UtcNow },
            CancellationToken.None);
        await _repo.SaveAsync(
            new AutomationHealthState { AutomationId = id, Health = AutomationHealth.Healthy },
            CancellationToken.None);

        var loaded = await _repo.GetAsync(id, CancellationToken.None);

        loaded.ShouldNotBeNull();
        loaded!.Health.ShouldBe(AutomationHealth.Healthy);
        loaded.WarningIssuedUtc.ShouldBeNull();
    }

    [Fact]
    public async Task GetManyAsync_ReturnsOnlyPresentRows()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var absent = Guid.NewGuid();

        await _repo.SaveAsync(new AutomationHealthState { AutomationId = a, Health = AutomationHealth.Disabled }, CancellationToken.None);
        await _repo.SaveAsync(new AutomationHealthState { AutomationId = b, Health = AutomationHealth.Degraded }, CancellationToken.None);

        var map = await _repo.GetManyAsync([a, b, absent], CancellationToken.None);

        map.Count.ShouldBe(2);
        map[a].Health.ShouldBe(AutomationHealth.Disabled);
        map[b].Health.ShouldBe(AutomationHealth.Degraded);
        map.ContainsKey(absent).ShouldBeFalse();
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
