using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Connection.Models;

/// <summary>
/// Request model for creating a new connection.
/// </summary>
public sealed class CreateConnectionRequestModel
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
