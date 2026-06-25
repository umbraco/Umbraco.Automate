using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Closes object schemas with <c>additionalProperties: false</c>, matching v17.
/// </summary>
/// <remarks>
/// Swashbuckle (v17) emitted <c>additionalProperties: false</c> on every generated object schema, so the
/// generated client treats the models as closed. Microsoft.AspNetCore.OpenApi (v18) leaves them open (no
/// <c>additionalProperties</c> keyword). We re-close them to preserve the v17 contract. Two cases are
/// deliberately left open, exactly as v17 produced:
/// <list type="bullet">
///   <item>Types that collect extra members via <see cref="JsonExtensionDataAttribute"/> (e.g.
///   <c>ProblemDetails</c>) — closing them would contradict their open-by-design contract.</item>
///   <item>Dictionaries — their schema kind is not <see cref="JsonTypeInfoKind.Object"/>, so they retain the
///   value schema in <c>additionalProperties</c> rather than <c>false</c>.</item>
/// </list>
/// </remarks>
internal sealed class ClosedObjectSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object
            && HasExtensionData(context.JsonTypeInfo.Type) is false)
        {
            schema.AdditionalPropertiesAllowed = false;
        }

        return Task.CompletedTask;
    }

    private static bool HasExtensionData(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => property.GetCustomAttribute<JsonExtensionDataAttribute>() is not null);
}
