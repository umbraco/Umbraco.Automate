namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// A parsed binding consisting of a data path and optional filter chain.
/// </summary>
internal sealed class BindingToken
{
    /// <summary>
    /// Gets the dot-separated data path (e.g. "trigger.contentName", "steps.sendEmail.messageId").
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets the ordered list of filters to apply.
    /// </summary>
    public IReadOnlyList<FilterToken> Filters { get; init; } = [];
}

/// <summary>
/// A parsed filter reference with arguments.
/// </summary>
internal sealed class FilterToken
{
    /// <summary>
    /// Gets the filter alias (e.g. "truncate", "formatDate").
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the filter arguments (e.g. ["100", "..."] for "truncate:100:...").
    /// </summary>
    public string[] Args { get; init; } = [];
}
