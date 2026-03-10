using Umbraco.Cms.Api.Common.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Generates OpenAPI schema IDs for Umbraco Automate model types.
/// </summary>
public class UmbracoAutomateApiSchemaIdHandler : SchemaIdHandler
{
    /// <inheritdoc />
    public override bool CanHandle(Type type)
        => type.Namespace?.StartsWith(Constants.AppNamespaceRoot) is true;
}
