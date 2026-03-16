namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Converts a string to lowercase. Usage: <c>| lowercase</c>.
/// </summary>
internal sealed class LowercaseFilter : IBindingFilter
{
    public string Alias => "lowercase";

    public object? Apply(object? value, string[] args)
        => value is string str ? str.ToLowerInvariant() : value;
}
