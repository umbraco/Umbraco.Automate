namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Converts a string to uppercase. Usage: <c>| uppercase</c>.
/// </summary>
internal sealed class UppercaseFilter : IBindingFilter
{
    public string Alias => "uppercase";

    public object? Apply(object? value, string[] args)
        => value is string str ? str.ToUpperInvariant() : value;
}
