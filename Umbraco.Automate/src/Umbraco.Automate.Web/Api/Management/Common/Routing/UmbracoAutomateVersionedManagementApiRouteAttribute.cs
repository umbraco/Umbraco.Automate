using Umbraco.Cms.Web.Common.Routing;

namespace Umbraco.Automate.Web.Api.Management.Common.Routing;

/// <summary>
/// Attribute for defining versioned Umbraco Automate Management API routes.
/// </summary>
/// <param name="template">The route template.</param>
public class UmbracoAutomateVersionedManagementApiRouteAttribute(string template)
    : BackOfficeRouteAttribute($"{Constants.ManagementApi.BackofficePath}/v{{version:apiVersion}}/{template.TrimStart('/')}");
