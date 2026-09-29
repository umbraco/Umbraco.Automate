namespace Umbraco.Automate.Core.Runs;

/// <summary>
/// Prepares stored run payloads — a step's input and output, a run's trigger data — for display:
/// masks sensitive values, pretty-prints the JSON and truncates it to
/// <see cref="MaxValueLength"/> characters. Works on a copy, so stored data is never changed.
/// </summary>
internal interface IRunDataSanitizer
{
    /// <summary>
    /// The maximum length, in characters, of a sanitized value. Longer values are cut and
    /// flagged with <see cref="SanitizedRunData.Truncated"/>.
    /// </summary>
    const int MaxValueLength = 64 * 1024;

    /// <summary>
    /// Sanitizes a step's recorded input. Besides the key patterns, fields the action's settings
    /// schema marks sensitive are masked.
    /// </summary>
    SanitizedRunData SanitizeStepInput(string actionAlias, string? inputJson);

    /// <summary>
    /// Sanitizes a step's recorded output.
    /// </summary>
    SanitizedRunData SanitizeStepOutput(string actionAlias, string? outputJson);

    /// <summary>
    /// Sanitizes a run's recorded trigger data.
    /// </summary>
    SanitizedRunData SanitizeTriggerData(string? triggerJson);
}

/// <summary>
/// A run payload prepared for display by <see cref="IRunDataSanitizer"/>.
/// </summary>
internal sealed class SanitizedRunData
{
    /// <summary>
    /// An empty result: nothing was recorded.
    /// </summary>
    public static SanitizedRunData Empty { get; } = new();

    /// <summary>
    /// Gets the masked, pretty-printed JSON, or <c>null</c> when nothing was recorded (or the stored
    /// value could not be read as JSON, in which case it is withheld rather than shown unmasked).
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets whether <see cref="Value"/> was cut to <see cref="IRunDataSanitizer.MaxValueLength"/> characters.
    /// </summary>
    public bool Truncated { get; init; }
}
