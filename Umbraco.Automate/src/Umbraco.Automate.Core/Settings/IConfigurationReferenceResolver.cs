namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Single owner of the <c>$Section:Path</c> configuration-reference concept: what counts as a
/// reference, which configuration prefixes are permitted, and how a reference resolves to a value.
/// </summary>
/// <remarks>
/// Both <see cref="EditableModelResolver"/> (which substitutes references at read time) and
/// <see cref="EditableModelSerializer"/> (which must not encrypt a value that is merely a pointer
/// to configuration) delegate here. Concentrating the allow-list, its normalisation and the
/// scanning algorithm behind one service means the two callers cannot drift apart on either
/// <em>how</em> a reference is parsed or <em>which</em> prefixes are allowed.
/// </remarks>
public interface IConfigurationReferenceResolver
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> contains at least one configuration
    /// reference whose key falls under an allowed prefix.
    /// </summary>
    /// <remarks>
    /// This deliberately consults only the <em>allowed</em> prefixes, never the secret sub-list —
    /// so "is this a reference?" and "may this reference resolve here?" have intentionally
    /// different answers. A value that points at configuration must stay plaintext regardless of
    /// whether the target is secret (secret-ness only constrains <see cref="Resolve"/>, i.e. which
    /// field a resolved secret may flow into). Do not add the secret check here.
    /// </remarks>
    bool ContainsReference(string? value);

    /// <summary>
    /// Resolves configuration references in <paramref name="value"/> and converts the result to
    /// <paramref name="targetType"/>.
    /// </summary>
    /// <param name="value">The property value to resolve. Non-string values are returned unchanged.</param>
    /// <param name="targetType">The target property type (used to convert whole-value references).</param>
    /// <param name="isSensitiveField">
    /// Whether the owning field is marked <c>[Field(IsSensitive = true)]</c>. Secret keys may only
    /// resolve into sensitive fields.
    /// </param>
    /// <returns>
    /// The original value when there is nothing to resolve; otherwise the value with references
    /// substituted (and, for a whole-value reference, converted to <paramref name="targetType"/>).
    /// </returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when a whole-value reference targets a disallowed prefix, when a referenced key is
    /// absent from configuration, or when a secret key is referenced from a non-sensitive field.
    /// </exception>
    object? Resolve(object? value, Type targetType, bool isSensitiveField);
}
