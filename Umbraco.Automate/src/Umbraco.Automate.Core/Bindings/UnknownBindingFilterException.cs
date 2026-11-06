namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Thrown when a <c>${ }</c> binding pipes its value through a filter alias that no registered
/// <see cref="IBindingFilter"/> declares, typically a typo such as <c>uppercse</c>. Derives from
/// <see cref="InvalidOperationException"/> so the step error classifier treats it as a terminal
/// configuration error: retrying cannot make the filter exist. The message names the filter and
/// the binding's data path but never the filter's arguments, which can hold literal text from a
/// sensitive field. The tokenizer splits on every <c>|</c>, so text after a pipe inside an argument
/// arrives here as an "alias"; one that isn't a well-formed name is not echoed.
/// </summary>
internal sealed class UnknownBindingFilterException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnknownBindingFilterException"/> class.
    /// </summary>
    /// <param name="filterAlias">The filter alias that matched no registered filter.</param>
    /// <param name="path">The data path of the binding the filter was applied to.</param>
    public UnknownBindingFilterException(string filterAlias, string path)
        : base(BindingTokenizer.IsWellFormedFilterAlias(filterAlias)
            ? $"The binding filter '{filterAlias}' used on '{path}' does not exist."
            : $"The binding on '{path}' uses a filter name that is not valid. A '|' inside a filter argument starts a new filter.")
    {
        FilterAlias = filterAlias;
    }

    /// <summary>
    /// Gets the filter alias that matched no registered filter.
    /// </summary>
    public string FilterAlias { get; }
}
