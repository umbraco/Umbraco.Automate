using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.OpenIddict.Credentials.Persistence;

namespace Umbraco.Automate.OpenIddict.Tests.Integration;

/// <summary>
/// Test fixture providing an in-memory SQLite database for OpenIddict repository tests.
/// </summary>
public sealed class OpenIddictTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<OpenIddictDbContext> _options;

    public OpenIddictTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public OpenIddictDbContext CreateContext() => new(_options);

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

/// <summary>
/// Test <see cref="IDbContextFactory{TContext}"/> backed by the test fixture.
/// </summary>
public sealed class TestDbContextFactory : IDbContextFactory<OpenIddictDbContext>
{
    private readonly OpenIddictTestFixture _fixture;

    public TestDbContextFactory(OpenIddictTestFixture fixture)
    {
        _fixture = fixture;
    }

    public OpenIddictDbContext CreateDbContext() => _fixture.CreateContext();
}

/// <summary>
/// Test protector that passes values through unmodified (no encryption).
/// </summary>
public sealed class PassthroughFieldProtector : ISensitiveFieldProtector
{
    public string? Protect(string? value) => value;
    public string? Unprotect(string? value) => value;
    public bool IsProtected(string? value) => false;
}
