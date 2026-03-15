using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Automations.Transfer;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Request model for importing an automation from a portable JSON definition.
/// </summary>
public sealed class ImportAutomationRequestModel
{
    /// <summary>
    /// Gets the target workspace to import the automation into.
    /// </summary>
    [Required]
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the export model to import.
    /// </summary>
    [Required]
    public required AutomationExportModel ExportModel { get; init; }
}
