using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="GetElementAction"/>.
/// </summary>
public sealed class GetElementOutput
{
    /// <summary>Gets the Library element's unique key.</summary>
    [Description("The Library element's unique key.")]
    public Guid ElementKey { get; init; }

    /// <summary>Gets the Library element's name in the requested culture, or its invariant name.</summary>
    [Description("The Library element's name in the requested culture, or its invariant name.")]
    public string? Name { get; init; }

    /// <summary>Gets the element type alias.</summary>
    [Description("The element type alias.")]
    public string? ContentTypeAlias { get; init; }

    /// <summary>Gets the element type key.</summary>
    [Description("The element type key.")]
    public Guid ContentTypeKey { get; init; }

    /// <summary>Gets the requested culture (null for invariant).</summary>
    [Description("The requested culture (null for invariant).")]
    public string? Culture { get; init; }

    /// <summary>Gets the cultures this element is published in.</summary>
    [Description("The cultures this element is published in.")]
    public string[] AvailableCultures { get; init; } = [];

    /// <summary>Gets creation timestamp (UTC).</summary>
    [Description("The creation timestamp (UTC).")]
    public DateTime CreateDate { get; init; }

    /// <summary>Gets the last-edited timestamp (UTC).</summary>
    [Description("The last-edited timestamp (UTC).")]
    public DateTime UpdateDate { get; init; }

    /// <summary>
    /// Gets the key of the user who created the element, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who created the Library element, if resolvable. Null if the user has been deleted.")]
    public Guid? CreatorKey { get; init; }

    /// <summary>
    /// Gets the key of the user who last edited the element, if resolvable.
    /// Null if the user has been deleted.
    /// </summary>
    [Description("The key of the user who last edited the Library element, if resolvable. Null if the user has been deleted.")]
    public Guid? WriterKey { get; init; }

    /// <summary>
    /// Gets the property values keyed by property alias, normalised for binding
    /// access. See <see cref="Cms.IContentValueNormaliser"/> for normalisation rules.
    /// </summary>
    [Description("The property values keyed by property alias, normalised for binding access.")]
    public IDictionary<string, object?> Properties { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
