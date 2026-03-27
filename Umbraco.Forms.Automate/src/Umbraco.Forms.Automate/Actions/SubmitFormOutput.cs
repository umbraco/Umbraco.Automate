namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Output produced by the <see cref="SubmitFormAction"/>.
/// </summary>
public sealed class SubmitFormOutput
{
    /// <summary>
    /// Gets the unique identifier of the created record.
    /// </summary>
    public Guid RecordUniqueId { get; init; }

    /// <summary>
    /// Gets the form ID the record was submitted to.
    /// </summary>
    public Guid FormId { get; init; }

    /// <summary>
    /// Gets the record's state after submission.
    /// </summary>
    public string? State { get; init; }
}
