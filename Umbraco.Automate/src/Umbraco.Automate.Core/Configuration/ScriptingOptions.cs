namespace Umbraco.Automate.Core.Configuration;

/// <summary>
/// Configuration options for the Run Script action's sandboxed JavaScript execution.
/// Bound to <c>Umbraco:Automate:Scripting</c> in appsettings.json.
/// </summary>
public sealed class ScriptingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Run Script action is enabled at all.
    /// When <c>false</c>, the action fails fast with a configuration error and scripts are
    /// rejected at save time — a tenant-wide kill switch. Default: <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether scripts may use <c>fetch</c> at all. This is a
    /// master switch: when <c>false</c>, <c>fetch</c> is never exposed regardless of a step's
    /// own <c>AllowFetch</c> toggle. Default: <c>true</c>.
    /// </summary>
    public bool FetchEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional allowlist of hosts a script's <c>fetch</c> may target. When
    /// empty, any host is allowed (subject to SSRF protection). When non-empty, only these
    /// hosts are permitted. Default: empty.
    /// </summary>
    public string[] FetchAllowedHosts { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum memory, in bytes, a single script may allocate. Default: 5 MB.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the maximum recursion depth a script may reach. Default: 64.
    /// </summary>
    public int MaxRecursionDepth { get; set; } = 64;

    /// <summary>
    /// Gets or sets the maximum array size a script may create. Default: 1000.
    /// </summary>
    public int MaxArraySize { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum number of statements a single script may execute. Default: 10,000.
    /// </summary>
    public int MaxStatements { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the per-statement timeout enforced by the engine. Default: 3 seconds.
    /// </summary>
    public TimeSpan StatementTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gets or sets the total wall-clock time a script may run before it is cancelled — the
    /// backstop against never-resolving promises. Capped by the step timeout at execution time.
    /// Default: 15 seconds.
    /// </summary>
    public TimeSpan TotalExecutionTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets the timeout for a single <c>fetch</c> HTTP request. Default: 5 seconds.
    /// </summary>
    public TimeSpan HttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum size, in bytes, of a <c>fetch</c> response body a script may
    /// read. Default: 10 MB.
    /// </summary>
    public long MaxResponseBodyBytes { get; set; } = 10_485_760;
}
