using Umbraco.Automate.Core.Bindings;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for inspecting string settings for <c>${ ... }</c> binding expressions.
/// </summary>
public static class BindingStringExtensions
{
    /// <summary>
    /// Determines whether a string contains at least one <c>${ ... }</c> binding expression,
    /// either as the whole value or embedded inside a larger template.
    /// </summary>
    /// <remarks>
    /// Intended for action authors computing outcomes from saved, unbound settings. A value that
    /// holds a binding has no known value until the step runs, so it can be skipped when
    /// building outcomes. Detection uses the same rules as the runtime binding evaluator.
    /// </remarks>
    /// <param name="value">The string to inspect.</param>
    /// <returns><c>true</c> if the string contains a binding; <c>false</c> for null, empty or plain text.</returns>
    public static bool ContainsBinding(this string? value)
        => !string.IsNullOrEmpty(value) && BindingTokenizer.FindBindings(value).Any();
}
