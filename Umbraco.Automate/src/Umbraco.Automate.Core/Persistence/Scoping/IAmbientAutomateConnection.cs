using System.Data.Common;

namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// Exposes the transaction of the ambient Umbraco scope, but only when Automate is configured
/// against the very same physical database as Umbraco CMS.
/// </summary>
/// <remarks>
/// This is the seam that lets <see cref="AmbientDbContextFactory{TDbContext}"/> decide between
/// enlisting in a caller's transaction and opening its own connection, without knowing anything
/// about Umbraco's scoping internals. It is shared across every Automate DbContext: whether
/// Automate targets the Umbraco database is a property of the host, not of one context.
/// </remarks>
internal interface IAmbientAutomateConnection
{
    /// <summary>
    /// Gets the ambient Umbraco scope's open transaction, or <c>null</c> when there is no ambient
    /// scope or when Automate targets a different database (in which case that transaction belongs
    /// to a connection that cannot see Automate's tables).
    /// </summary>
    DbTransaction? Transaction { get; }
}
