namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// The result of asking an <see cref="ISupportsManualRun"/> trigger for stand-in output
/// for an on-demand run. Either output was produced (possibly none, when the trigger needs
/// no payload) or the trigger's saved settings could not produce any, with a reason to show
/// the author.
/// </summary>
public sealed record ManualRunOutput
{
    private ManualRunOutput(Dictionary<string, object?>? data, string? error)
    {
        Data = data;
        Error = error;
    }

    /// <summary>
    /// Gets the stand-in trigger output to expose to the automation's steps, or <c>null</c>
    /// when the trigger needs none.
    /// </summary>
    public Dictionary<string, object?>? Data { get; }

    /// <summary>
    /// Gets the reason the output could not be built, or <c>null</c> when it was.
    /// Shown to the author, so it should say what to fix and where.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a value indicating whether output was produced. <c>true</c> even when
    /// <see cref="Data"/> is <c>null</c> — a trigger that needs no payload still succeeds.
    /// </summary>
    public bool Success => Error is null;

    /// <summary>
    /// The trigger needs no stand-in output — the automation simply begins.
    /// </summary>
    public static readonly ManualRunOutput None = new(null, null);

    /// <summary>
    /// Stand-in output the automation's steps should see in place of the real payload.
    /// </summary>
    public static ManualRunOutput From(Dictionary<string, object?>? data) => new(data, null);

    /// <summary>
    /// The trigger's saved settings cannot produce output; <paramref name="error"/> says why.
    /// </summary>
    public static ManualRunOutput Invalid(string error) => new(null, error);
}
