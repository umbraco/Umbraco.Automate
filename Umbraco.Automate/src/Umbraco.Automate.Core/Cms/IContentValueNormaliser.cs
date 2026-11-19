using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Normalises Umbraco published content values into binding-friendly shapes for use
/// in automation step outputs. Summarises tree nodes (pickers), recurses into embedded
/// elements (nested content, blocks) and applies the CMS's configured language
/// fallback when reading properties.
/// </summary>
public interface IContentValueNormaliser
{
    /// <summary>
    /// Normalises a single already-resolved property value.
    /// </summary>
    /// <param name="value">The value to normalise.</param>
    /// <param name="culture">The culture context for summarising/expanding nested items.</param>
    object? Normalise(object? value, string? culture);

    /// <summary>
    /// Reads all property values from a content node, applying the configured language
    /// fallback and normalising each into a binding-friendly shape.
    /// </summary>
    /// <param name="content">The content node to read.</param>
    /// <param name="culture">The culture to read, or null for invariant.</param>
    IDictionary<string, object?> NormaliseProperties(IPublishedContent content, string? culture);

    /// <summary>
    /// Reads a single property from a content node, applying the configured language
    /// fallback and normalising the result into a binding-friendly shape.
    /// </summary>
    /// <param name="content">The content node to read.</param>
    /// <param name="alias">The property alias to read.</param>
    /// <param name="culture">The culture to read, or null for invariant.</param>
    object? ReadProperty(IPublishedContent content, string alias, string? culture);

    /// <summary>
    /// Reads all property values from a published element (for example a Library element),
    /// applying the configured language fallback and normalising each into the same
    /// binding-friendly shape used for content.
    /// </summary>
    /// <remarks>
    /// The default implementation forwards an <see cref="IPublishedContent"/> to
    /// <see cref="NormaliseProperties(IPublishedContent, string?)"/> and throws for any other element,
    /// so existing implementers keep compiling.
    /// </remarks>
    /// <param name="element">The element to read.</param>
    /// <param name="culture">The culture to read, or null for invariant.</param>
    /// <exception cref="NotSupportedException">The implementation does not support plain elements.</exception>
    // TODO (V19): Remove the default implementation.
    // The default keeps existing implementations of this interface compiling. Plain elements cannot be read through the content members, so it throws.
    IDictionary<string, object?> NormaliseProperties(IPublishedElement element, string? culture)
        => element is IPublishedContent content
            ? NormaliseProperties(content, culture)
            : throw new NotSupportedException(
                $"{GetType().Name} does not support normalising {nameof(IPublishedElement)} values.");

    /// <summary>
    /// Reads a single property from a published element, applying the configured language
    /// fallback and normalising the result into the same shape used for content.
    /// </summary>
    /// <remarks>
    /// The default implementation forwards an <see cref="IPublishedContent"/> to
    /// <see cref="ReadProperty(IPublishedContent, string, string?)"/> and throws for any other element,
    /// so existing implementers keep compiling.
    /// </remarks>
    /// <param name="element">The element to read.</param>
    /// <param name="alias">The property alias to read.</param>
    /// <param name="culture">The culture to read, or null for invariant.</param>
    /// <exception cref="NotSupportedException">The implementation does not support plain elements.</exception>
    // TODO (V19): Remove the default implementation.
    // The default keeps existing implementations of this interface compiling. Plain elements cannot be read through the content members, so it throws.
    object? ReadProperty(IPublishedElement element, string alias, string? culture)
        => element is IPublishedContent content
            ? ReadProperty(content, alias, culture)
            : throw new NotSupportedException(
                $"{GetType().Name} does not support normalising {nameof(IPublishedElement)} values.");
}
