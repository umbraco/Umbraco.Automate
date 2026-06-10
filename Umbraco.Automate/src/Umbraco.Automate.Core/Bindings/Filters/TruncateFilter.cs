namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Truncates a string to a maximum length. Usage: <c>| truncate:100</c> or <c>| truncate:100:...</c>.
/// </summary>
internal sealed class TruncateFilter : IBindingFilter
{
    public string Alias => "truncate";

    public object? Apply(object? value, string[] args)
    {
        if (value is not string str || args.Length == 0)
        {
            return value;
        }

        if (!int.TryParse(args[0], out var maxLength) || str.Length <= maxLength)
        {
            return value;
        }

        var suffix = args.Length > 1 ? args[1] : string.Empty;
        return string.Concat(str.AsSpan(0, maxLength), suffix);
    }
}
