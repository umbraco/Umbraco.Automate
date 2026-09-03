namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// A <c>console</c> shim exposed to scripts. Each method forwards to <see cref="Logger"/> with the
/// level name, so the host can route script logging into the automation run log.
/// </summary>
internal sealed class JsConsole
{
    /// <summary>
    /// Gets or sets the callback invoked for every <c>console</c> call, receiving the level
    /// (e.g. "log", "warn") and the arguments passed by the script.
    /// </summary>
    public required Action<string, IReadOnlyList<object>> Logger { get; set; }

    /// <summary>Writes a debug-level message.</summary>
    public void Debug(params object[] data) => Logger("debug", data);

    /// <summary>Writes an error-level message.</summary>
    public void Error(params object[] data) => Logger("error", data);

    /// <summary>Writes an info-level message.</summary>
    public void Info(params object[] data) => Logger("info", data);

    /// <summary>Writes a log-level message.</summary>
    public void Log(params object[] data) => Logger("log", data);

    /// <summary>Writes a trace-level message.</summary>
    public void Trace(params object[] data) => Logger("trace", data);

    /// <summary>Writes a warning-level message.</summary>
    public void Warn(params object[] data) => Logger("warn", data);
}
