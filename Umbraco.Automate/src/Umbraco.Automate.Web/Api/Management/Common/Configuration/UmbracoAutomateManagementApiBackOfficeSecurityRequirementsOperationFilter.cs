using Umbraco.Cms.Api.Management.OpenApi;

namespace Umbraco.Automate.Web.Api.Management.Common.Configuration;

/// <summary>
/// Adds backoffice security requirements to Umbraco Automate Management API operations.
/// </summary>
public class UmbracoAutomateManagementApiBackOfficeSecurityRequirementsOperationFilter
    : BackOfficeSecurityRequirementsOperationFilterBase
{
    /// <inheritdoc />
    protected override string ApiName => Constants.ManagementApi.ApiName;
}
