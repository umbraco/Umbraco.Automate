namespace Umbraco.Automate.Core.Expressions.Filters;

/// <summary>
/// Converts a string to uppercase. Usage: <c>| uppercase</c>.
/// </summary>
internal sealed class UppercaseFilter : IExpressionFilter
{
    public string Alias => "uppercase";

    public object? Apply(object? value, string[] args)
        => value is string str ? str.ToUpperInvariant() : value;
}
