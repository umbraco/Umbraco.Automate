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
/// <see cref="Description"/> is optional and follows the same <c>#key</c>-or-literal rule as <see cref="Label"/>.
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

    /// <summary>
    /// Gets an optional explanation of when the action finishes with this outcome, shown to authors as
    /// the tooltip on the outcome's exit. It is a <c>#key</c> localization key or literal text, and is
    /// always shown as text.
    /// </summary>
    public string? Description { get; init; }
}
