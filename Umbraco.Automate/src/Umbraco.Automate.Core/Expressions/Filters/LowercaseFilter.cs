namespace Umbraco.Automate.Core.Expressions.Filters;

/// <summary>
/// Converts a string to lowercase. Usage: <c>| lowercase</c>.
/// </summary>
internal sealed class LowercaseFilter : IExpressionFilter
{
    public string Alias => "lowercase";

    public object? Apply(object? value, string[] args)
        => value is string str ? str.ToLowerInvariant() : value;
}
