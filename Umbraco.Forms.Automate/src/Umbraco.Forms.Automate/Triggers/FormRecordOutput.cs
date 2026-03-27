namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Output produced by form record triggers (submitted, approved).
/// </summary>
public sealed class FormRecordOutput
{
    /// <summary>
    /// Gets the form's unique identifier.
    /// </summary>
    public Guid FormId { get; init; }

    /// <summary>
    /// Gets the form's name.
    /// </summary>
    public string? FormName { get; init; }

    /// <summary>
    /// Gets the record's unique identifier.
    /// </summary>
    public Guid RecordUniqueId { get; init; }

    /// <summary>
    /// Gets the record's state (e.g. Submitted, Approved).
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Gets the submission timestamp.
    /// </summary>
    public DateTime Created { get; init; }

    /// <summary>
    /// Gets the submitter's IP address.
    /// </summary>
    public string? IP { get; init; }

    /// <summary>
    /// Gets the associated Umbraco member key, if any.
    /// </summary>
    public string? MemberKey { get; init; }

    /// <summary>
    /// Gets the submission culture.
    /// </summary>
    public string? Culture { get; init; }

    /// <summary>
    /// Gets the record field values as a JSON string (field GUID to value mapping).
    /// </summary>
    public string? RecordFieldsJson { get; init; }
}
