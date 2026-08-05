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
    private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
    private readonly IConfiguration _configuration;

    // Latched once the answer is knowable — see SharesUmbracoDatabase. _latched is volatile so a
    // thread that reads it as true also sees the _sharesUmbracoDatabase write that preceded it.
    private volatile bool _latched;
    private bool _sharesUmbracoDatabase;

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
        _connectionStrings = connectionStrings;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public DbTransaction? Transaction
    {
        get
        {
            if (!SharesUmbracoDatabase())
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

    /// <summary>
    /// Determines whether Automate is configured against the same physical database as Umbraco CMS.
    /// </summary>
    /// <remarks>
    /// The answer is cached, because it would otherwise run on the path of every single Automate
    /// query — but only once it is actually knowable. A connection string that is missing at the
    /// moment of asking does not mean "separate database": the install wizard writes both entries at
    /// run time, and hosts that synthesise a DSN (Umbraco Cloud, Umbraco Deploy) populate it through
    /// the options pipeline after boot. Latching "no" on that transient state would leave the process
    /// permanently on the detached path — with the SQLite deadlock this class exists to avoid — until
    /// the next restart. So an unknowable answer is returned uncached and asked again next time.
    /// </remarks>
    private bool SharesUmbracoDatabase()
    {
        if (_latched)
        {
            return _sharesUmbracoDatabase;
        }

        string automateConnectionString;
        string automateProviderName;
        try
        {
            (automateConnectionString, automateProviderName) =
                DatabaseConnectionInfo.Resolve(_connectionStrings, _configuration);
        }
        catch (InvalidOperationException)
        {
            // Automate has no connection string configured yet. Nothing to share, and the real error
            // surfaces from the DbContext factory rather than from here.
            return false;
        }

        // Read after resolving Automate's side, which forces the ConnectionStrings options pipeline
        // to run — that is what populates a host-synthesised DSN for the CMS entry too.
        ConnectionStrings umbraco = _connectionStrings.CurrentValue;
        if (string.IsNullOrWhiteSpace(umbraco.ConnectionString))
        {
            return false;
        }

        var shares = AutomateDatabaseTarget.IsSameDatabase(
            umbraco.ConnectionString,
            umbraco.ProviderName,
            automateConnectionString,
            automateProviderName);

        _sharesUmbracoDatabase = shares;
        _latched = true;

        return shares;
    }
}
