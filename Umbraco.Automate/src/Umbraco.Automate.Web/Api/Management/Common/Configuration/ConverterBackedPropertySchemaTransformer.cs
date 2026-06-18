using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Restores schema types for properties whose runtime serialization is driven by a custom
/// <see cref="System.Text.Json.Serialization.JsonConverter"/> on the API's JSON options.
/// </summary>
/// <remarks>
/// Microsoft.AspNetCore.OpenApi cannot infer a schema for a value handled by a custom converter, so it emits an
/// untyped schema that the generated client surfaces as <c>unknown</c>. Automate registers its converters
/// (<see cref="Json.UtcDateTimeJsonConverter"/>, <see cref="Json.UtcNullableDateTimeJsonConverter"/>,
/// <see cref="Json.JsonStringTypeConverter"/>) globally rather than via per-property <c>[JsonConverter]</c>
/// attributes, so we match on the property's CLR type rather than <see cref="JsonPropertyInfo.CustomConverter"/>
/// (which only reflects per-property attributes). This restores the v17 (Swashbuckle) output where
/// <see cref="DateTime"/> rendered as a date-time string and <see cref="Type"/> rendered as a string.
/// </remarks>
internal sealed class ConverterBackedPropertySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (schema.Properties is null || context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return Task.CompletedTask;
        }

        foreach (JsonPropertyInfo property in context.JsonTypeInfo.Properties)
        {
            if (schema.Properties.TryGetValue(property.Name, out IOpenApiSchema? propertySchema) is false
                || propertySchema is not OpenApiSchema concrete)
            {
                continue;
            }

            Type underlying = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (underlying == typeof(DateTime))
            {
                var nullable = Nullable.GetUnderlyingType(property.PropertyType) is not null;
                concrete.Type = nullable ? JsonSchemaType.String | JsonSchemaType.Null : JsonSchemaType.String;
                concrete.Format = "date-time";
            }
            else if (typeof(Type).IsAssignableFrom(underlying))
            {
                concrete.Type = JsonSchemaType.String;
                concrete.Format = null;
            }
        }

        return Task.CompletedTask;
    }
}
