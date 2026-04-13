using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Default implementation of <see cref="IContentValueNormaliser"/>. Walks converted
/// property values from <see cref="IPublishedContent"/> and produces binding-friendly
/// shapes — summarises tree nodes (pickers), recurses into embedded elements, and
/// flattens block editor models into walkable arrays.
/// </summary>
public sealed class ContentValueNormaliser : IContentValueNormaliser
{
    /// <summary>
    /// Maximum depth for recursing into embedded elements / blocks. Guards against
    /// runaway expansion through self-referential nested blocks.
    /// </summary>
    private const int MaxExpansionDepth = 5;

    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IPublishedUrlProvider _urlProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentValueNormaliser"/> class.
    /// </summary>
    public ContentValueNormaliser(
        IPublishedValueFallback publishedValueFallback,
        IPublishedUrlProvider urlProvider)
    {
        _publishedValueFallback = publishedValueFallback;
        _urlProvider = urlProvider;
    }

    /// <inheritdoc />
    public object? Normalise(object? value, string? culture)
        => NormaliseInternal(value, culture, depth: 0);

    /// <inheritdoc />
    public IDictionary<string, object?> NormaliseProperties(IPublishedContent content, string? culture)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in content.Properties)
        {
            var raw = content.Value(
                _publishedValueFallback,
                prop.Alias,
                culture,
                fallback: Fallback.ToLanguage);

            result[prop.Alias] = NormaliseInternal(raw, culture, depth: 0);
        }

        return result;
    }

    /// <inheritdoc />
    public object? ReadProperty(IPublishedContent content, string alias, string? culture)
    {
        var raw = content.Value(
            _publishedValueFallback,
            alias,
            culture,
            fallback: Fallback.ToLanguage);

        return NormaliseInternal(raw, culture, depth: 0);
    }

    private object? NormaliseInternal(object? value, string? culture, int depth)
    {
        if (depth >= MaxExpansionDepth)
        {
            return null;
        }

        return value switch
        {
            null => null,

            // HTML → plain string; the binding walker can't drill into IHtmlEncodedString.
            IHtmlEncodedString html => html.ToHtmlString(),

            // Block editor models — flatten to arrays of walkable block shapes. Grid
            // and rich-text blocks differ in structure from list blocks so each has its
            // own arm. These must come BEFORE the generic IEnumerable arms below because
            // BlockModelCollection<T> is ultimately IEnumerable<IBlockReference<...>>.
            BlockGridModel grid
                => grid.Select(b => ExpandGridBlock(b, culture, depth + 1)).ToArray(),
            BlockListModel list
                => list.Select(b => ExpandBlock(b, b.SettingsKey, culture, depth + 1)).ToArray(),
            RichTextBlockModel rtBlocks
                => rtBlocks.Select(b => ExpandBlock(b, b.SettingsKey, culture, depth + 1)).ToArray(),

            // Tree nodes (pickers, links) — summarise. Don't recurse; users who want the
            // target's properties can chain another Get Content action on its key.
            // IPublishedContent cases must appear BEFORE the IPublishedElement cases
            // because IPublishedContent : IPublishedElement.
            IPublishedContent picked => SummarisePicked(picked, culture),
            IEnumerable<IPublishedContent> pickedMany
                => pickedMany.Select(p => SummarisePicked(p, culture)).ToArray(),

            // Embedded elements (nested content, block content/settings) — recurse so
            // bindings can reach their properties directly.
            IPublishedElement element => ExpandElement(element, culture, depth + 1),
            IEnumerable<IPublishedElement> elements
                => elements.Select(e => ExpandElement(e, culture, depth + 1)).ToArray(),

            // Everything else — pass through. Primitives, strings, dates, Guids, and any
            // custom converter outputs are handled here. The binding walker and JSON
            // serialiser deal with them on a best-effort basis.
            _ => value,
        };
    }

    private IDictionary<string, object?> SummarisePicked(IPublishedContent picked, string? culture)
        => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = picked.Key,
            ["name"] = GetName(picked, culture),
            ["contentTypeAlias"] = picked.ContentType.Alias,
            ["url"] = picked.Url(_urlProvider, culture, UrlMode.Default),
        };

    // Resolves the culture-specific name without requiring an IVariationContextAccessor.
    // Falls back to the invariant Name property when the requested culture has no entry.
    private static string? GetName(IPublishedContent content, string? culture)
    {
        if (culture is not null && content.Cultures.TryGetValue(culture, out var info))
        {
            return info.Name;
        }

        return content.Name;
    }

    private IDictionary<string, object?> ExpandElement(IPublishedElement element, string? culture, int depth)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["key"] = element.Key,
            ["contentTypeAlias"] = element.ContentType.Alias,
        };

        foreach (var prop in element.Properties)
        {
            var raw = element.Value(
                _publishedValueFallback,
                prop.Alias,
                culture,
                fallback: Fallback.ToLanguage);

            result[prop.Alias] = NormaliseInternal(raw, culture, depth);
        }

        return result;
    }

    // SettingsKey lives on the concrete block item types, not on IBlockReference, so
    // callers pass it explicitly. Content/Settings are accessed through the interface.
    private IDictionary<string, object?> ExpandBlock(
        IBlockReference<IPublishedElement, IPublishedElement> block,
        Guid? settingsKey,
        string? culture,
        int depth)
        => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["contentKey"] = block.ContentKey,
            ["settingsKey"] = settingsKey,
            ["content"] = ExpandElement(block.Content, culture, depth),
            ["settings"] = block.Settings is null
                ? null
                : ExpandElement(block.Settings, culture, depth),
        };

    private IDictionary<string, object?> ExpandGridBlock(BlockGridItem block, string? culture, int depth)
    {
        var result = ExpandBlock(block, block.SettingsKey, culture, depth);

        result["rowSpan"] = block.RowSpan;
        result["columnSpan"] = block.ColumnSpan;
        result["areas"] = block.Areas
            .Select(area => (object)new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["alias"] = area.Alias,
                ["rowSpan"] = area.RowSpan,
                ["columnSpan"] = area.ColumnSpan,
                ["items"] = area.Select(item => ExpandGridBlock(item, culture, depth + 1)).ToArray(),
            })
            .ToArray();

        return result;
    }
}
