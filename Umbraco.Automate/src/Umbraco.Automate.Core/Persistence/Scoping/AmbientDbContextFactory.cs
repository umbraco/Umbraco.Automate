using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// <see cref="IDbContextFactory{TContext}"/> decorator that enlists an Automate DbContext's work in
/// the ambient Umbraco scope's transaction when that context shares the Umbraco CMS database, and
/// otherwise defers to the pooled factory it wraps.
/// </summary>
/// <typeparam name="TDbContext">The Automate DbContext this factory produces.</typeparam>
/// <remarks>
/// <para>
/// Without this, every Automate read and write opens a second, independent connection. On SQL Server
/// that merely costs atomicity — a caller's transaction can roll back while Automate's writes stay
/// committed. On SQLite it deadlocks outright, because SQLite allows a single writer per database
/// <em>file</em>: a caller that holds the write lock for the duration of its own work (Umbraco Deploy's
/// restore holds it for the whole restore, via <c>EagerWriteLock</c>) can never release it, because it
/// is itself waiting on the Automate write that is queued behind that lock. The wait is only broken by
/// the command timeout, surfacing as <c>SQLite Error 5: 'database is locked'</c>.
/// </para>
/// <para>
/// Enlisting is only safe when both sides address the same physical database, so the decision is
/// delegated to <see cref="IAmbientAutomateConnection"/>. When it reports no transaction — no ambient
/// scope, or a separate Automate database — behaviour is byte-for-byte what it was before: a pooled,
/// detached context.
/// </para>
/// <para>
/// The enlisted context is deliberately <em>not</em> taken from the pool. A pooled context that has had
/// a foreign connection attached has to have its own connection string restored before it goes back,
/// or it is handed to the next caller unusable (see
/// <see href="https://github.com/umbraco/Umbraco-CMS/issues/22211">Umbraco-CMS#22211</see>).
/// Constructing a throwaway context sidesteps that entirely, and it only happens on the enlisted path.
/// </para>
/// <para>
/// Engine internals that must not be tied to a caller's transaction — the WorkflowCore persistence
/// provider, the workflow lock store, the outbox dispatcher — already run with
/// <c>ExecutionContext.SuppressFlow</c>, so no ambient scope reaches them and they keep taking the
/// detached path.
/// </para>
/// </remarks>
internal sealed class AmbientDbContextFactory<TDbContext> : IDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly IDbContextFactory<TDbContext> _detachedFactory;
    private readonly IAmbientAutomateConnection _ambientConnection;
    private readonly Func<DbConnection, TDbContext> _createEnlistedContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmbientDbContextFactory{TDbContext}"/> class.
    /// </summary>
    /// <param name="detachedFactory">The pooled factory used whenever there is no transaction to enlist in.</param>
    /// <param name="ambientConnection">Supplies the ambient Umbraco transaction, when there is one to share.</param>
    /// <param name="createEnlistedContext">
    /// Builds a context bound to the supplied connection. Owned by the product rather than by this
    /// type, because each DbContext has its own provider configuration and interceptors — and those
    /// must mirror the pooled factory's, so a write behaves the same on both paths.
    /// </param>
    public AmbientDbContextFactory(
        IDbContextFactory<TDbContext> detachedFactory,
        IAmbientAutomateConnection ambientConnection,
        Func<DbConnection, TDbContext> createEnlistedContext)
    {
        _detachedFactory = detachedFactory;
        _ambientConnection = ambientConnection;
        _createEnlistedContext = createEnlistedContext;
    }

    /// <inheritdoc />
    public TDbContext CreateDbContext()
        => TryCreateEnlistedContext() ?? _detachedFactory.CreateDbContext();

    /// <inheritdoc />
    public async Task<TDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => TryCreateEnlistedContext()
            ?? await _detachedFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    private TDbContext? TryCreateEnlistedContext()
    {
        DbTransaction? transaction = _ambientConnection.Transaction;
        if (transaction?.Connection is not DbConnection connection)
        {
            return null;
        }

        TDbContext context = _createEnlistedContext(connection);

        try
        {
            // The connection is owned by the ambient scope, so disposing this context leaves it open
            // and leaves the transaction for its owner to commit or roll back.
            context.Database.UseTransaction(transaction);
        }
        catch
        {
            context.Dispose();
            throw;
        }

        return context;
    }
}
