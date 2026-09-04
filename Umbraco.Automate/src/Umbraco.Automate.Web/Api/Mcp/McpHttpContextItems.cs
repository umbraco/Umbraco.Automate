namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> keys the MCP authentication
/// middleware populates for the MCP server's per-request session configuration to read,
/// so the automation and its settings are resolved from the database exactly once per request.
/// </summary>
internal static class McpHttpContextItems
{
    public const string AutomationKey = "Umbraco.Automate.Mcp.Automation";

    public const string SettingsKey = "Umbraco.Automate.Mcp.Settings";
}
