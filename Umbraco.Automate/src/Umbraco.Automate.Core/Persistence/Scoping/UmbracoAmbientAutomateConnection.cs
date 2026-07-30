using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// <see cref="IAmbientAutomateConnection"/> backed by Umbraco's ambient scope.
/// </summary>
internal sealed class UmbracoAmbientAutomateConnection : IAmbientAutomateConnection
{
    private readonly Lazy<IScopeAccessor> _scopeAccessor;
    private readonly Lazy<bool> _sharesUmbracoDatabase;

    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoAmbientAutomateConnection"/> class.
    /// </summary>
    /// <remarks>
    /// <see cref="IScopeAccessor"/> is taken lazily, mirroring Umbraco's own locking mechanisms:
    /// this type is resolved while the persistence layer is still being composed, and eagerly
    /// resolving the scope accessor from there closes a dependency cycle.
    /// </remarks>
    public UmbracoAmbientAutomateConnection(
        Lazy<IScopeAccessor> scopeAccessor,
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        IConfiguration configuration)
    {
        _scopeAccessor = scopeAccessor;

        // Resolved once: neither connection string can change without a restart, and the check
        // would otherwise run on the path of every single Automate query.
        _sharesUmbracoDatabase = new Lazy<bool>(
            () => SharesUmbracoDatabase(connectionStrings, configuration));
    }

    /// <inheritdoc />
    public DbTransaction? Transaction
    {
        get
        {
            if (!_sharesUmbracoDatabase.Value)
            {
                return null;
            }

            // Reading Database on an ambient scope materialises its connection and transaction if
            // the scope has not done any database work yet. That matches what Umbraco's own EF Core
            // scoping does, and on a shared database the write we are about to make would have
            // needed that transaction open regardless.
            return _scopeAccessor.Value.AmbientScope?.Database.Transaction;
        }
    }

    private static bool SharesUmbracoDatabase(
        IOptionsMonitor<ConnectionStrings> connectionStrings,
        IConfiguration configuration)
    {
        ConnectionStrings umbraco = connectionStrings.CurrentValue;

        try
        {
            var (automateConnectionString, automateProviderName) =
                DatabaseConnectionInfo.Resolve(connectionStrings, configuration);

            return AutomateDatabaseTarget.IsSameDatabase(
                umbraco.ConnectionString,
                umbraco.ProviderName,
                automateConnectionString,
                automateProviderName);
        }
        catch (InvalidOperationException)
        {
            // Automate has no connection string configured at all. Nothing to share, and the real
            // error surfaces from the DbContext factory rather than from here.
            return false;
        }
    }
}
