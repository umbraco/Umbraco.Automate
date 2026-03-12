namespace Umbraco.Automate.Persistence.Connections;

/// <summary>
/// EF Core entity for the connection table.
/// </summary>
internal sealed class ConnectionEntity
{
    public Guid Id { get; set; }

    public required string Alias { get; set; }

    public required string Name { get; set; }

    public required string Type { get; set; }

    /// <summary>
    /// Settings serialised as a JSON document. Sensitive values are encrypted.
    /// </summary>
    public string? Settings { get; set; }

    public int Version { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
