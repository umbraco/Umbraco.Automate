using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Umbraco.Automate.Persistence;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// Test fixture that provides an in-memory SQLite database for EF Core repository tests.
/// </summary>
public class EfCoreTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Creates a new <see cref="UmbracoAutomateDbContext"/> instance for testing.
    /// </summary>
    public UmbracoAutomateDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new UmbracoAutomateDbContext(options);
    }

    /// <summary>
    /// Initializes the test fixture with an in-memory SQLite database.
    /// </summary>
    public EfCoreTestFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Disposes the database connection.
    /// </summary>
    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
