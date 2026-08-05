namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// The per-provider migrations assemblies of one Automate DbContext.
/// </summary>
/// <param name="SqlServer">The assembly holding the SQL Server migrations.</param>
/// <param name="Sqlite">The assembly holding the SQLite migrations.</param>
/// <remarks>
/// Both are needed at configuration time because the provider is only known at run time, and a
/// DbContext cannot be configured for one provider with the other's migrations.
/// </remarks>
internal readonly record struct AutomateMigrationsAssemblies(string SqlServer, string Sqlite);
