using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for workspace persistence via real EF Core + in-memory SQLite.
/// </summary>
public class WorkspacePersistenceTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture;
    private readonly EFCoreWorkspaceRepository _repository;

    public WorkspacePersistenceTests()
    {
        _fixture = new EfCoreTestFixture();
        var dbContextFactory = new TestDbContextFactory(_fixture.CreateContext);
        _repository = new EFCoreWorkspaceRepository(dbContextFactory);
    }

    [Fact]
    public async Task SaveAndGet_RoundTrips()
    {
        Workspace workspace = new WorkspaceBuilder()
            .WithAlias("test-roundtrip")
            .WithName("Roundtrip Workspace")
            .WithServiceAccountKey(Guid.NewGuid())
            .Build();

        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetAsync(workspace.Id);

        loaded.ShouldNotBeNull();
        loaded.Alias.ShouldBe("test-roundtrip");
        loaded.Name.ShouldBe("Roundtrip Workspace");
        loaded.ServiceAccountKey.ShouldBe(workspace.ServiceAccountKey);
        loaded.Version.ShouldBe(1);
    }

    [Fact]
    public async Task SaveAndGet_PreservesUserGroups()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        Workspace workspace = new WorkspaceBuilder()
            .WithUserGroups(groupA, groupB)
            .Build();

        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetAsync(workspace.Id);

        loaded.ShouldNotBeNull();
        loaded.UserGroups.ShouldBe(new[] { groupA, groupB }, ignoreOrder: true);
    }

    [Fact]
    public async Task SaveAndGet_PreservesAllowedConnections()
    {
        var connA = Guid.NewGuid();
        var connB = Guid.NewGuid();

        Workspace workspace = new WorkspaceBuilder()
            .WithAllowedConnections(connA, connB)
            .Build();

        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetAsync(workspace.Id);

        loaded.ShouldNotBeNull();
        loaded.AllowedConnections.ShouldBe(new[] { connA, connB }, ignoreOrder: true);
    }

    [Fact]
    public async Task Update_IncrementsVersion()
    {
        Workspace workspace = new WorkspaceBuilder().Build();
        await _repository.SaveAsync(workspace);

        workspace.Name = "Updated Name";
        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetAsync(workspace.Id);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Updated Name");
        loaded.Version.ShouldBe(2);
    }

    [Fact]
    public async Task Update_ReplacesUserGroups()
    {
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();
        var groupC = Guid.NewGuid();

        Workspace workspace = new WorkspaceBuilder()
            .WithUserGroups(groupA, groupB)
            .Build();

        await _repository.SaveAsync(workspace);

        workspace.UserGroups = [groupC];
        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetAsync(workspace.Id);

        loaded.ShouldNotBeNull();
        loaded.UserGroups.Count.ShouldBe(1);
        loaded.UserGroups.ShouldContain(groupC);
    }

    [Fact]
    public async Task Delete_RemovesWorkspace()
    {
        Workspace workspace = new WorkspaceBuilder().Build();
        await _repository.SaveAsync(workspace);

        var deleted = await _repository.DeleteAsync(workspace.Id);

        deleted.ShouldBeTrue();
        (await _repository.GetAsync(workspace.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Delete_CascadesJoinTableRows()
    {
        Workspace workspace = new WorkspaceBuilder()
            .WithUserGroups(Guid.NewGuid())
            .WithAllowedConnections(Guid.NewGuid())
            .Build();

        await _repository.SaveAsync(workspace);
        await _repository.DeleteAsync(workspace.Id);

        // If cascade failed, re-inserting same ID would conflict on join tables.
        Workspace replacement = new WorkspaceBuilder()
            .WithId(workspace.Id)
            .WithAlias(workspace.Alias + "-2")
            .Build();

        await _repository.SaveAsync(replacement);
        (await _repository.GetAsync(workspace.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByAlias_ReturnsCorrectWorkspace()
    {
        Workspace workspace = new WorkspaceBuilder()
            .WithAlias("unique-alias")
            .Build();

        await _repository.SaveAsync(workspace);

        var loaded = await _repository.GetByAliasAsync("unique-alias");

        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(workspace.Id);
    }

    [Fact]
    public async Task GetIdsByUserGroupKeys_ReturnsMatchingWorkspaces()
    {
        var sharedGroup = Guid.NewGuid();
        var otherGroup = Guid.NewGuid();

        Workspace ws1 = new WorkspaceBuilder().WithUserGroups(sharedGroup).Build();
        Workspace ws2 = new WorkspaceBuilder().WithUserGroups(otherGroup).Build();

        await _repository.SaveAsync(ws1);
        await _repository.SaveAsync(ws2);

        var ids = await _repository.GetIdsByUserGroupKeysAsync([sharedGroup]);

        ids.ShouldContain(ws1.Id);
        ids.ShouldNotContain(ws2.Id);
    }

    [Fact]
    public async Task GetPaged_FiltersAndPaginates()
    {
        Workspace ws1 = new WorkspaceBuilder().WithName("Alpha Workspace").WithAlias("alpha").Build();
        Workspace ws2 = new WorkspaceBuilder().WithName("Beta Workspace").WithAlias("beta").Build();
        Workspace ws3 = new WorkspaceBuilder().WithName("Alpha Two").WithAlias("alpha-two").Build();

        await _repository.SaveAsync(ws1);
        await _repository.SaveAsync(ws2);
        await _repository.SaveAsync(ws3);

        var (items, total) = await _repository.GetPagedAsync("Alpha", skip: 0, take: 10);

        total.ShouldBe(2);
        items.Count().ShouldBe(2);
    }

    public void Dispose() => _fixture.Dispose();
}
