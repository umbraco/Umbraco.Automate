namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Provides a default value when the input is null or empty. Usage: <c>| fallback:default-value</c>.
/// </summary>
internal sealed class FallbackFilter : IBindingFilter
{
    public string Alias => "fallback";

    public object? Apply(object? value, string[] args)
    {
        if (args.Length == 0)
        {
            return value;
        }

        if (value is null || (value is string str && string.IsNullOrEmpty(str)))
        {
            return args[0];
        }

        return value;
    }
}
