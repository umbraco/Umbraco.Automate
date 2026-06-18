using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Collapses the v18 numeric-as-string representation back to a plain numeric schema, matching v17.
/// </summary>
/// <remarks>
/// With <see cref="System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString"/> in effect,
/// Microsoft.AspNetCore.OpenApi widens numeric types to <c>type: ["integer"/"number","string"]</c> with a numeric
/// pattern (to allow string-encoded values). Swashbuckle (v17) emitted a plain numeric type and the generated
/// client typed these as <c>number</c>; keeping the string member widens them to <c>number | string</c> and
/// breaks every consumer. Strip the string member (and the now-redundant pattern) to preserve the v17 shape.
/// </remarks>
internal sealed class NumericStringUnionSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Type is { } type
            && type.HasFlag(JsonSchemaType.String)
            && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
        {
            schema.Type = type & ~JsonSchemaType.String;
            schema.Pattern = null;
        }

        return Task.CompletedTask;
    }
}
