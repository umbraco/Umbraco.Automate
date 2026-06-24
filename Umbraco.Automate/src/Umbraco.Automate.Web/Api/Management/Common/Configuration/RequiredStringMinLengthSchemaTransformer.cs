using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Emits <c>minLength: 1</c> for string properties carrying <see cref="RequiredAttribute"/>, matching v17.
/// </summary>
/// <remarks>
/// Swashbuckle (v17) honoured <c>[Required]</c> on a <see cref="string"/> property by emitting
/// <c>minLength: 1</c> (a required string cannot be empty). Microsoft.AspNetCore.OpenApi (v18) drops the
/// constraint, so the generated client and any schema-validating consumer lose it. We restore it to preserve the
/// v17 contract. The signal is the explicit <see cref="RequiredAttribute"/>, not nullability: models that make a
/// string required only via the C# <c>required</c> keyword / non-nullable reference types (e.g. the export
/// transfer models) appear in <c>required</c> but never received <c>minLength</c> from v17, so they are left
/// untouched. Gating on <see cref="string"/> also means value types that serialise as strings (e.g.
/// <see cref="System.Guid"/>) carrying <c>[Required]</c> are correctly ignored, as in v17.
/// </remarks>
internal sealed class RequiredStringMinLengthSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonPropertyInfo is { PropertyType: var propertyType } property
            && propertyType == typeof(string)
            && property.AttributeProvider?.IsDefined(typeof(RequiredAttribute), inherit: true) is true)
        {
            schema.MinLength = 1;
        }

        return Task.CompletedTask;
    }
}
