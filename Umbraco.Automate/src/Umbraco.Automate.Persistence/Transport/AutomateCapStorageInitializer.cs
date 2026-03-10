using DotNetCore.CAP;
using DotNetCore.CAP.Persistence;
using DotNetCore.CAP.SqlServer;
using DotNetCore.CAP.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// Custom CAP storage initializer for SQL Server that names tables
/// <c>umbracoAutomateOutboxPublished</c>, <c>umbracoAutomateOutboxReceived</c>, etc.
/// so they sit alongside other <c>umbracoAutomate*</c> tables in the <c>dbo</c> schema.
/// </summary>
internal sealed class SqlServerCapStorageInitializer(
    ILogger<SqlServerStorageInitializer> logger,
    IOptions<SqlServerOptions> options,
    IOptions<CapOptions> capOptions)
    : SqlServerStorageInitializer(logger, options, capOptions)
{
    private const string Prefix = "umbracoAutomateOutbox";

    public override string GetPublishedTableName() => $"[dbo].[{Prefix}Published]";

    public override string GetReceivedTableName() => $"[dbo].[{Prefix}Received]";

    public override string GetLockTableName() => $"[dbo].[{Prefix}Lock]";
}

/// <summary>
/// Custom CAP storage initializer for SQLite that names tables
/// <c>umbracoAutomateOutboxPublished</c>, <c>umbracoAutomateOutboxReceived</c>, etc.
/// for consistency with other <c>umbracoAutomate*</c> tables.
/// </summary>
/// <remarks>
/// The base <see cref="SqliteStorageInitializer"/> seals the table name methods,
/// so we implement <see cref="IStorageInitializer"/> directly and override
/// <see cref="SqliteStorageInitializer.CreateDbTablesScript"/> for DDL generation.
/// https://github.com/colinin/DotNetCore.CAP.Sqlite/blob/master/src/DotNetCore.CAP.Sqlite/IStorageInitializer.Sqlite.cs
/// </remarks>
internal sealed class SqliteCapStorageInitializer : SqliteStorageInitializer, IStorageInitializer
{
    private const string Prefix = "umbracoAutomateOutbox";
    private readonly IOptions<CapOptions> _capOptions;

    public SqliteCapStorageInitializer(
        ILogger<SqliteStorageInitializer> logger,
        IOptions<SqliteOptions> options,
        IOptions<CapOptions> capOptions)
        : base(logger, options, capOptions)
    {
        _capOptions = capOptions;
    }

    // These hide the sealed base members but satisfy the IStorageInitializer interface
    // when resolved as IStorageInitializer (which is how CAP resolves it).
    // Return names WITHOUT backticks — CAP's SqliteDataStorage wraps them in backticks itself.
    string IStorageInitializer.GetPublishedTableName() => $"{Prefix}Published";

    string IStorageInitializer.GetReceivedTableName() => $"{Prefix}Received";

    string IStorageInitializer.GetLockTableName() => $"{Prefix}Locks";

    protected override string CreateDbTablesScript()
    {
        var published = $"`{Prefix}Published`";
        var received = $"`{Prefix}Received`";

        var sql = $"""
            CREATE TABLE IF NOT EXISTS {received} (
              `Id` bigint NOT NULL,
              `Version` varchar(20) DEFAULT NULL COLLATE NOCASE,
              `Name` varchar(400) NOT NULL COLLATE NOCASE,
              `Group` varchar(200) DEFAULT NULL COLLATE NOCASE,
              `Content` longtext,
              `Retries` int(11) DEFAULT NULL,
              `Added` datetime NOT NULL,
              `ExpiresAt` datetime DEFAULT NULL,
              `StatusName` varchar(50) NOT NULL COLLATE NOCASE,
              PRIMARY KEY (`Id`)
            );
            CREATE INDEX IF NOT EXISTS `IX_{Prefix}Received_Version_ExpiresAt_StatusName` ON {received}(`Version`, `ExpiresAt`, `StatusName`);
            CREATE INDEX IF NOT EXISTS `IX_{Prefix}Received_ExpiresAt_StatusName` ON {received}(`ExpiresAt`, `StatusName`);

            CREATE TABLE IF NOT EXISTS {published} (
              `Id` bigint NOT NULL,
              `Version` varchar(20) DEFAULT NULL COLLATE NOCASE,
              `Name` varchar(200) NOT NULL COLLATE NOCASE,
              `Content` longtext,
              `Retries` int(11) DEFAULT NULL,
              `Added` datetime NOT NULL,
              `ExpiresAt` datetime DEFAULT NULL,
              `StatusName` varchar(40) NOT NULL COLLATE NOCASE,
              PRIMARY KEY (`Id`)
            );
            CREATE INDEX IF NOT EXISTS `IX_{Prefix}Published_Version_ExpiresAt_StatusName` ON {published}(`Version`, `ExpiresAt`, `StatusName`);
            CREATE INDEX IF NOT EXISTS `IX_{Prefix}Published_ExpiresAt_StatusName` ON {published}(`ExpiresAt`, `StatusName`);
            """;

        if (_capOptions.Value.UseStorageLock)
        {
            var locks = $"`{Prefix}Locks`";
            sql += $"""

                CREATE TABLE IF NOT EXISTS {locks} (
                  `Key` varchar(128) NOT NULL COLLATE NOCASE,
                  `Instance` varchar(256) DEFAULT NULL COLLATE NOCASE,
                  `LastLockTime` datetime DEFAULT NULL,
                  PRIMARY KEY (`Key`)
                );

                INSERT OR IGNORE INTO {locks} (`Key`,`Instance`,`LastLockTime`) VALUES(@PubKey, '', @LastLockTime);
                INSERT OR IGNORE INTO {locks} (`Key`,`Instance`,`LastLockTime`) VALUES(@RecKey, '', @LastLockTime);
                """;
        }

        return sql;
    }
}
