using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Test fixture that provides an in-memory SQLite database for EF Core repository tests.
/// Uses a named shared-cache database so each <see cref="UmbracoAutomateDbContext"/> gets
/// its own connection, making concurrent access from multiple threads safe.
/// </summary>
public class EfCoreTestFixture : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    /// <summary>
    /// Creates a new <see cref="UmbracoAutomateDbContext"/> instance for testing.
    /// Each call opens a separate connection to the shared in-memory database.
    /// </summary>
    public UmbracoAutomateDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        return new UmbracoAutomateDbContext(options);
    }

    /// <summary>
    /// Initializes the test fixture with an in-memory SQLite database.
    /// </summary>
    public EfCoreTestFixture()
    {
        var dbName = $"test_{Guid.NewGuid():N}";
        _connectionString = $"DataSource=file:{dbName}?mode=memory&cache=shared";

        // Keep one connection open so the in-memory database persists for
        // the lifetime of the fixture.
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Disposes the database connection.
    /// </summary>
    public void Dispose()
    {
        _keepAliveConnection.Close();
        _keepAliveConnection.Dispose();
        GC.SuppressFinalize(this);
    }
}
