namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// A named result an action can finish with. Authors route each outcome to its own next step.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Key"/> is persisted on automation connections, so keep it stable once released.
/// </para>
/// <para>
/// <see cref="Label"/> is shown to authors. It may be a <c>#key</c> localization key or literal text.
/// </para>
/// <para>
/// A step type declares at most one default outcome. A default is only needed when the action can
/// succeed without naming an outcome.
/// </para>
/// </remarks>
/// <param name="Key">The stable identifier of the outcome.</param>
/// <param name="Label">The display label: a <c>#key</c> localization key or literal text.</param>
public sealed record StepOutcome(string Key, string Label)
{
    /// <summary>
    /// Gets whether this is the default outcome, used when the action succeeds without naming an outcome.
    /// </summary>
    public bool IsDefault { get; init; }
}
