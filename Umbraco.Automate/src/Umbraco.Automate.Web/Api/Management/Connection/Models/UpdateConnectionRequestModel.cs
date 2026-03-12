using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Connection.Models;

/// <summary>
/// Request model for updating an existing connection.
/// </summary>
public sealed class UpdateConnectionRequestModel
{
    /// <summary>The unique alias.</summary>
    [Required]
    public required string Alias { get; init; }

    /// <summary>The display name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>The connection type alias (e.g. "slack", "smtp").</summary>
    [Required]
    public required string Type { get; init; }

    /// <summary>The connection settings.</summary>
    public Dictionary<string, object?> Settings { get; init; } = [];
}
