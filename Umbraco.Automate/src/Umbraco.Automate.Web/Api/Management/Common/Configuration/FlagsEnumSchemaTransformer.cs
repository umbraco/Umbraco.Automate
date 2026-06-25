using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Describes <c>[Flags]</c> enums as string enums of their individual member names.
/// </summary>
/// <remarks>
/// The API's JSON options serialise enums via <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>.
/// For a <c>[Flags]</c> enum this yields comma-separated member names for combined values, so the OpenAPI
/// generator can only describe it as a free-form <c>string</c> with no values — which strips the named members
/// from the generated client (e.g. <c>NotifyOnModel</c> collapsing to <c>string</c>). We restore them from the
/// enum's declared member names, matching the v17 (Swashbuckle) output. This mirrors the equivalent transformer
/// in Umbraco Forms.
/// </remarks>
internal sealed class FlagsEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Type type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;

        if (type.IsEnum is false || type.GetCustomAttribute<FlagsAttribute>() is null)
        {
            return Task.CompletedTask;
        }

        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Enum = Enum.GetNames(type)
            .Select(name => (JsonNode)JsonValue.Create(name)!)
            .ToList();

        return Task.CompletedTask;
    }
}
