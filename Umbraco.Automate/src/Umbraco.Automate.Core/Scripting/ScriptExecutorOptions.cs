namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// Options controlling a single <see cref="ScriptExecutor.ExecuteAsync"/> invocation.
/// </summary>
public sealed class ScriptExecutorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the <c>fetch</c> global is exposed to the script.
    /// </summary>
    public bool AllowFetch { get; set; }

    /// <summary>
    /// Gets or sets an optional allowlist of hosts <c>fetch</c> may target. Empty allows any host
    /// (still subject to SSRF protection).
    /// </summary>
    public IReadOnlyCollection<string> FetchAllowedHosts { get; set; } = [];

    /// <summary>Gets or sets the maximum memory, in bytes, a script may allocate. Default: 5 MB.</summary>
    public long MaxMemoryBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Gets or sets the maximum recursion depth. Default: 64.</summary>
    public int MaxRecursionDepth { get; set; } = 64;

    /// <summary>Gets or sets the maximum array size. Default: 1000.</summary>
    public int MaxArraySize { get; set; } = 1000;

    /// <summary>Gets or sets the maximum number of statements. Default: 10,000.</summary>
    public int MaxStatements { get; set; } = 10_000;

    /// <summary>Gets or sets the per-statement engine timeout. Default: 3 seconds.</summary>
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the timeout for a single <c>fetch</c> HTTP request. Default: 5 seconds.
    /// </summary>
    public TimeSpan HttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the total wall-clock time a script may run before it is cancelled. This is the
    /// backstop that terminates scripts the engine's per-statement timeout cannot (e.g. a
    /// never-resolving promise). Default: 15 seconds.
    /// </summary>
    public TimeSpan TotalExecutionTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the maximum size, in bytes, of a <c>fetch</c> response body a script may read.
    /// Default: 10 MB.
    /// </summary>
    public long MaxResponseBodyBytes { get; set; } = 10_485_760;

    /// <summary>
    /// Gets or sets a callback invoked when execution fails, with the classified error.
    /// </summary>
    public Action<ScriptError>? OnError { get; set; }

    /// <summary>
    /// Gets or sets a callback invoked for every <c>console</c> call made by the script.
    /// </summary>
    public Action<LogMessage>? OnLogMessage { get; set; }
}

/// <summary>
/// Classifies why a script failed, so callers can map it to an appropriate outcome.
/// </summary>
public enum ScriptErrorKind
{
    /// <summary>The script could not be compiled or imported (e.g. a syntax error, or no default export).</summary>
    Compilation,

    /// <summary>The script exceeded its total execution time or a Jint resource limit.</summary>
    Timeout,

    /// <summary>The script threw an uncaught error at runtime.</summary>
    Runtime,

    /// <summary>An unexpected host error occurred while executing the script.</summary>
    Unexpected,
}

/// <summary>
/// A classified script execution failure.
/// </summary>
/// <param name="Kind">The failure classification.</param>
/// <param name="Message">A human-readable message.</param>
public readonly record struct ScriptError(ScriptErrorKind Kind, string Message);

/// <summary>
/// A single <c>console</c> message emitted by a script.
/// </summary>
/// <param name="Level">The console level (e.g. "log", "warn", "error").</param>
/// <param name="Message">The formatted message text.</param>
public readonly record struct LogMessage(string Level, string Message);
