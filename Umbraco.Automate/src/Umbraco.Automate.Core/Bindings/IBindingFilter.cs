using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// A chainable filter that transforms values in bindings.
/// Filters are applied via the pipe syntax: <c>${ path | filterAlias:arg1:arg2 }</c>.
/// </summary>
public interface IBindingFilter : IDiscoverable
{
    /// <summary>
    /// Gets the alias used in bindings (e.g. "truncate", "lowercase").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Applies the filter to a value.
    /// </summary>
    /// <param name="value">The current value (may be null).</param>
    /// <param name="args">Arguments passed after the filter alias, split by colon.</param>
    /// <returns>The transformed value.</returns>
    object? Apply(object? value, string[] args);
}
