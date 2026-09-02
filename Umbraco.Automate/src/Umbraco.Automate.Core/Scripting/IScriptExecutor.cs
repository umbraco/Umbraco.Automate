using System.Text.Json.Nodes;

namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// Executes user-authored JavaScript in a sandboxed engine.
/// </summary>
public interface IScriptExecutor
{
    /// <summary>
    /// Executes <paramref name="script"/>, invoking its <c>default</c> export with
    /// <paramref name="data"/> and returning the (promise-unwrapped) result.
    /// </summary>
    /// <param name="scriptName">A name for the module (used in error messages).</param>
    /// <param name="script">The JavaScript module source.</param>
    /// <param name="data">The data passed as the single argument to the default export.</param>
    /// <param name="options">Execution options (limits, fetch, timeouts, callbacks).</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>
    /// The result serialized to JSON-compatible data (via <c>JSON.stringify</c> semantics:
    /// functions and <c>undefined</c> become <c>null</c>, <c>NaN</c>/<c>Infinity</c> become
    /// <c>null</c>, dates become ISO strings, and circular references fail as a runtime error).
    /// <c>null</c> if execution failed (see <see cref="ScriptExecutorOptions.OnError"/>).
    /// </returns>
    ValueTask<JsonNode?> ExecuteAsync(
        string scriptName,
        string script,
        JsonNode? data,
        ScriptExecutorOptions options,
        CancellationToken cancellationToken = default);
}
