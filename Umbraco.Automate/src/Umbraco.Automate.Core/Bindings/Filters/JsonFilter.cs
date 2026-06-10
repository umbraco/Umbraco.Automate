using System.Text.Json;

namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Serializes the value to JSON. Usage: <c>| json</c>.
/// </summary>
internal sealed class JsonFilter : IBindingFilter
{
    public string Alias => "json";

    public object? Apply(object? value, string[] args)
        => value is null ? "null" : JsonSerializer.Serialize(value);
}
