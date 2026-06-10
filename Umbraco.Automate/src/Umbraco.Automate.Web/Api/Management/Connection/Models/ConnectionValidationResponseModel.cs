namespace Umbraco.Automate.Web.Api.Management.Connection.Models;

/// <summary>
/// Response shape for the connection test endpoint. Mirrors
/// <c>Umbraco.Automate.Core.Connections.ConnectionValidationResult</c> over the wire.
/// </summary>
public sealed class ConnectionValidationResponseModel
{
    /// <summary>Gets or sets the outcome status (success, warning, failure).</summary>
    public required ConnectionValidationStatusModel Status { get; set; }

    /// <summary>Gets or sets the primary user-facing message.</summary>
    public string? Message { get; set; }

    /// <summary>Gets or sets supporting detail lines.</summary>
    public IReadOnlyList<string> Details { get; set; } = [];
}

/// <summary>
/// Mirrors <c>Umbraco.Automate.Core.Connections.ConnectionValidationStatus</c>.
/// </summary>
public enum ConnectionValidationStatusModel
{
    /// <summary>Credentials verified against the external system.</summary>
    Success,

    /// <summary>No concrete check was available.</summary>
    Warning,

    /// <summary>The external system rejected the credentials or could not be reached.</summary>
    Failure,
}
