using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by <see cref="GetConnectionsAction"/>.
/// </summary>
public sealed class GetConnectionsOutput
{
    /// <summary>Gets the connections available to the automation's workspace, ordered by name.</summary>
    [Description("The connections available to this workspace, ordered by name.")]
    public IReadOnlyList<GetConnectionsItem> Connections { get; init; } = [];

    /// <summary>Gets the number of connections returned.</summary>
    [Description("The number of connections returned.")]
    public int Count => Connections.Count;

    /// <summary>
    /// Gets the first connection, if any. Convenience for automations that expect a single
    /// match, so a downstream step can bind <c>${ steps.alias.first.id }</c> without a loop.
    /// </summary>
    [Description("The first connection, if any — a convenience for automations that expect a single match.")]
    public GetConnectionsItem? First => Connections.Count > 0 ? Connections[0] : null;
}
