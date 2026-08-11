using System.Data.Common;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// Decides whether two connection strings address the same physical database, which is the
/// precondition for Automate enlisting in a caller's Umbraco transaction.
/// </summary>
/// <remarks>
/// Raw string comparison is not enough: the same database is routinely spelled differently on the
/// two sides. Umbraco resolves <c>|DataDirectory|</c> before handing its connection string to EF
/// Core while Automate reads its own straight from configuration, keyword order and casing differ
/// between a hand-written <c>appsettings.json</c> entry and a host-synthesised one (Umbraco Cloud
/// and Deploy both generate theirs at run time), and SQL Server accepts several spellings for both
/// the server and the catalog. So each side is reduced to a canonical target and those are compared.
/// </remarks>
internal static class AutomateDatabaseTarget
{
    /// <summary>
    /// Determines whether Automate's connection addresses the same database as Umbraco CMS.
    /// </summary>
    /// <remarks>
    /// Errs towards <c>false</c>: an unrecognised provider or an unparseable connection string means
    /// "assume separate", which keeps the pre-existing detached behaviour rather than risking
    /// Automate's writes landing on a connection that cannot see its tables.
    /// </remarks>
    internal static bool IsSameDatabase(
        string? umbracoConnectionString,
        string? umbracoProviderName,
        string? automateConnectionString,
        string? automateProviderName)
    {
        DatabaseProvider umbracoProvider = ParseProvider(umbracoProviderName);
        if (umbracoProvider == DatabaseProvider.Unknown ||
            umbracoProvider != ParseProvider(automateProviderName))
        {
            return false;
        }

        var umbracoTarget = Describe(umbracoConnectionString, umbracoProvider);
        var automateTarget = Describe(automateConnectionString, umbracoProvider);

        return umbracoTarget is not null &&
               string.Equals(umbracoTarget, automateTarget, ComparisonFor(umbracoProvider));
    }

    /// <summary>
    /// Picks the comparison the provider's identifiers actually use.
    /// </summary>
    /// <remarks>
    /// A SQLite target is a file, and file names are case-sensitive everywhere except Windows: on
    /// Linux and macOS two paths differing only in case are two different databases, so matching
    /// them case-insensitively would be exactly the false positive this class exists to avoid. The
    /// reverse mistake is the safe one — a missed match keeps the pre-existing detached behaviour —
    /// so a case-insensitive volume on a case-sensitive platform is deliberately not accommodated.
    /// SQL Server server and catalog names are case-insensitive on every platform.
    /// </remarks>
    private static StringComparison ComparisonFor(DatabaseProvider provider)
        => provider == DatabaseProvider.Sqlite && !OperatingSystem.IsWindows()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    private static string? Describe(string? connectionString, DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException)
        {
            return null;
        }

        return provider switch
        {
            DatabaseProvider.Sqlite => DescribeSqlite(builder),
            DatabaseProvider.SqlServer => DescribeSqlServer(builder),
            _ => null,
        };
    }

    /// <summary>
    /// A SQLite database <em>is</em> its file, so the canonical target is that file's full path.
    /// </summary>
    private static string? DescribeSqlite(DbConnectionStringBuilder builder)
    {
        var dataSource = Read(builder, "data source", "datasource", "filename");
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return null;
        }

        // In-memory databases are identified by name, not by a path — never normalise those.
        if (dataSource.StartsWith(":memory:", StringComparison.OrdinalIgnoreCase) ||
            dataSource.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
        {
            return $"memory:{dataSource}";
        }

        var dataDirectory = AppDomain.CurrentDomain
            .GetData(UmbracoConstants.System.DataDirectoryName)?.ToString();

        if (!string.IsNullOrEmpty(dataDirectory))
        {
            dataSource = dataSource.Replace(
                UmbracoConstants.System.DataDirectoryPlaceholder,
                dataDirectory,
                StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            return $"file:{Path.GetFullPath(dataSource)}";
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unresolved |DataDirectory| placeholder lands here. Treat it as separate rather
            // than guessing at the path.
            return null;
        }
    }

    private static string? DescribeSqlServer(DbConnectionStringBuilder builder)
    {
        var server = Read(builder, "data source", "server", "address", "addr", "network address");
        var catalog = Read(builder, "initial catalog", "database");

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(catalog))
        {
            // LocalDB with AttachDbFilename and no catalog, or anything else we cannot pin down.
            var attachedFile = Read(builder, "attachdbfilename", "extended properties", "initial file name");

            return string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(attachedFile)
                ? null
                : $"{Normalise(server)}|file:{attachedFile.Trim()}";
        }

        return $"{Normalise(server)}|{catalog.Trim()}";
    }

    /// <summary>
    /// Collapses the spellings that address the same SQL Server instance: the network-library prefix,
    /// an explicitly stated default port, and the several ways of naming the local machine. So
    /// <c>tcp:(local),1433\SQLEXPRESS</c> and <c>localhost\SQLEXPRESS</c> are recognised as one server.
    /// </summary>
    /// <remarks>
    /// This matters most where the fix matters most. On Umbraco Cloud the CMS connection string is
    /// generated by the host while Automate's is written by hand, so the two sides routinely spell the
    /// same server differently — and an unrecognised match silently drops back to the detached path.
    /// Anything not collapsed here still fails safe, just uselessly.
    /// </remarks>
    private static string Normalise(string server)
    {
        var trimmed = server.Trim();

        // Network library prefix: tcp:, np: (named pipes), lpc: (shared memory).
        foreach (var prefix in NetworkLibraryPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..].TrimStart();
                break;
            }
        }

        // A host and a named instance are separated by a backslash, and only the host part has
        // synonyms — SQLEXPRESS and sqlexpress are the same instance, .\SQLEXPRESS and
        // localhost\SQLEXPRESS the same server.
        var separator = trimmed.IndexOf('\\');
        var host = separator < 0 ? trimmed : trimmed[..separator];
        var instance = separator < 0 ? string.Empty : trimmed[separator..];

        // A stated default port addresses the same endpoint as no port at all. Any other port does
        // not, so it stays part of the host.
        if (host.EndsWith(DefaultPortSuffix, StringComparison.Ordinal))
        {
            host = host[..^DefaultPortSuffix.Length].TrimEnd();
        }

        if (host is "." or "(local)" or "localhost")
        {
            host = "localhost";
        }

        return host + instance;
    }

    private static readonly string[] NetworkLibraryPrefixes = ["tcp:", "np:", "lpc:"];

    private const string DefaultPortSuffix = ",1433";

    private static string? Read(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text)
            {
                return text;
            }
        }

        return null;
    }

    private static DatabaseProvider ParseProvider(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return DatabaseProvider.Unknown;
        }

        // Both families have more than one accepted spelling: Microsoft.Data.Sqlite vs
        // Microsoft.Data.SQLite, and Microsoft.Data.SqlClient vs the legacy System.Data.SqlClient
        // that Umbraco Cloud and Azure still emit.
        if (providerName.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseProvider.Sqlite;
        }

        return providerName.Contains("sqlclient", StringComparison.OrdinalIgnoreCase)
            ? DatabaseProvider.SqlServer
            : DatabaseProvider.Unknown;
    }

    private enum DatabaseProvider
    {
        Unknown,
        Sqlite,
        SqlServer,
    }
}
