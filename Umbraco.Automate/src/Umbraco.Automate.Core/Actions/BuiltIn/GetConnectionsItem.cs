using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A single connection returned by <see cref="GetConnectionsAction"/>. Deliberately limited to
/// non-sensitive metadata: a connection's settings can hold API keys, tokens or passwords, so
/// they are never part of a step's output.
/// </summary>
public sealed class GetConnectionsItem
{
    /// <summary>Gets the connection's unique ID.</summary>
    [Description("The connection's unique ID.")]
    public Guid Id { get; init; }

    /// <summary>Gets the connection's name.</summary>
    [Description("The connection's name.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the connection's alias.</summary>
    [Description("The connection's alias.")]
    public string Alias { get; init; } = string.Empty;

    /// <summary>Gets the connection type alias.</summary>
    [Description("The connection type alias.")]
    public string TypeAlias { get; init; } = string.Empty;
}
