using Microsoft.EntityFrameworkCore;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// An <see cref="IDbContextFactory{TContext}"/> that always produces a context on its own connection,
/// never enlisted in a caller's ambient Umbraco transaction.
/// </summary>
/// <typeparam name="TDbContext">The Automate DbContext this factory produces.</typeparam>
/// <remarks>
/// <para>
/// <see cref="AmbientDbContextFactory{TDbContext}"/> replaces the plain
/// <see cref="IDbContextFactory{TContext}"/> registration, so everything that resolves the factory
/// enlists whenever an ambient scope happens to be open. That is what the domain repositories want:
/// a caller's transaction should own their writes.
/// </para>
/// <para>
/// It is emphatically not what the engine's own bookkeeping wants. A workflow instance, an execution
/// pointer or a lock lease written inside a caller's transaction is invisible to the engine's
/// background workers until that caller commits, and disappears entirely if it rolls back — while the
/// in-memory engine carries on believing the row is there. Depending on
/// <c>ExecutionContext.SuppressFlow</c> to keep those writes off the enlisted path makes the
/// invariant a property of every call site, forever. Asking for this type instead makes it a property
/// of the store.
/// </para>
/// </remarks>
internal interface IDetachedDbContextFactory<TDbContext> : IDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
}
