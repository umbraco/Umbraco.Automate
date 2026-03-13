using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Web.Api.Management.Approval.Models;

/// <summary>
/// Request model for submitting an approval decision.
/// </summary>
public sealed class ApprovalDecisionRequestModel
{
    /// <summary>
    /// Gets or sets the decision outcome.
    /// </summary>
    [Required]
    public required ApprovalOutcome Outcome { get; init; }

    /// <summary>
    /// Gets or sets an optional comment.
    /// </summary>
    public string? Comment { get; init; }
}
