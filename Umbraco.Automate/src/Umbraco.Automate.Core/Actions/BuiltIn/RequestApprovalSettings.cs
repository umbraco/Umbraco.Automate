using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the Request Approval action.
/// </summary>
public sealed class RequestApprovalSettings
{
    /// <summary>
    /// Gets or sets the approval prompt message shown to approvers.
    /// </summary>
    [Field(Label = "Prompt", Description = "The message shown to approvers explaining what needs approval.", SupportsBindings = true)]
    public string? Prompt { get; set; }

    /// <summary>
    /// Gets or sets an optional timeout after which the step auto-rejects.
    /// </summary>
    [Field(Label = "Timeout (hours)", Description = "Optional: auto-reject after this many hours. Leave empty for no timeout.", SortOrder = 1)]
    public int? TimeoutHours { get; set; }
}
