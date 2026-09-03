using System.Text.Json.Nodes;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="RunScriptAction"/>.
/// </summary>
public sealed class RunScriptOutput
{
    /// <summary>
    /// Gets the value returned by the script's default export, as JSON (objects, arrays, and
    /// primitives) that downstream steps can bind to.
    /// </summary>
    public JsonNode? Result { get; init; }
}
