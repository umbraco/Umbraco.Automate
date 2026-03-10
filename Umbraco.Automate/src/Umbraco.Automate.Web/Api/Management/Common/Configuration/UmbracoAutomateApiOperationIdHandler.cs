using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Generates OpenAPI operation IDs for Umbraco Automate Management API controllers.
/// </summary>
public class UmbracoAutomateApiOperationIdHandler(IOptions<ApiVersioningOptions> apiVersioningOptions)
    : OperationIdHandler(apiVersioningOptions)
{
    /// <inheritdoc />
    protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
        => controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith(Constants.AppNamespaceRoot) is true;
}
