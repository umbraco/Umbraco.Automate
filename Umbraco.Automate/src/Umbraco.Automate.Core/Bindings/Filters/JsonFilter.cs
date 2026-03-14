using System.Text.Json;

namespace Umbraco.Automate.Core.Expressions.Filters;

/// <summary>
/// Serializes the value to JSON. Usage: <c>| json</c>.
/// </summary>
internal sealed class JsonFilter : IExpressionFilter
{
    public string Alias => "json";

    public object? Apply(object? value, string[] args)
        => value is null ? "null" : JsonSerializer.Serialize(value);
}
