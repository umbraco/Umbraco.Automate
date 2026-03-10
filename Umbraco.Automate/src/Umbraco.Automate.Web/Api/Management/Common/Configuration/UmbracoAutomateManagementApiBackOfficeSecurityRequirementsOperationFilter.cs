using Umbraco.Cms.Api.Management.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Adds backoffice security requirements to Umbraco Automate Management API operations.
/// </summary>
internal sealed class UmbracoAutomateManagementApiBackOfficeSecurityRequirementsOperationFilter(string apiName)
    : BackOfficeSecurityRequirementsOperationFilterBase
{
    /// <inheritdoc />
    protected override string ApiName => apiName;
}
