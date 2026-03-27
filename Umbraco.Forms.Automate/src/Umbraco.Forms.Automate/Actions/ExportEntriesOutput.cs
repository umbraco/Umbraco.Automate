namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Output produced by the <see cref="ExportEntriesAction"/>.
/// </summary>
public sealed class ExportEntriesOutput
{
    /// <summary>
    /// Gets the form ID entries were exported from.
    /// </summary>
    public Guid FormId { get; init; }

    /// <summary>
    /// Gets the total number of records matching the query.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Gets the number of records in this export.
    /// </summary>
    public int ExportedCount { get; init; }

    /// <summary>
    /// Gets the exported entries as a JSON array.
    /// </summary>
    public string? EntriesJson { get; init; }
}
