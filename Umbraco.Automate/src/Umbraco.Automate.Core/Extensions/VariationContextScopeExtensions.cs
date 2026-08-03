using Umbraco.Cms.Core.Models.PublishedContent;

namespace Umbraco.Automate.Extensions;

internal static class VariationContextScopeExtensions
{
    /// <summary>
    /// Establishes an ambient <see cref="VariationContext"/> for the duration of the returned
    /// scope, restoring the previous one on dispose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Required before reading published property values outside an HTTP request. Umbraco
    /// resolves a missing culture or segment from <see cref="IVariationContextAccessor.VariationContext"/>,
    /// which is null on a background thread — <c>EnsureUmbracoContext</c> does not set one.
    /// A null segment then reaches <c>CompositeStringStringKey</c> inside
    /// <c>PublishedProperty.GetSourceValue</c>, which rejects it with
    /// "Value cannot be null. (Parameter 'key2')" for any property type that varies by segment.
    /// A null culture fails the same way as <c>key1</c>.
    /// </para>
    /// <para>
    /// <see cref="VariationContext"/> normalises both values to <see cref="string.Empty"/>, which
    /// is what makes the lookup safe. This mirrors how the CMS handles its own non-request reads
    /// (see <c>ExtendedContentWebhookEventBase.BuildCultureProperties</c> and <c>PublishedRouter</c>).
    /// </para>
    /// </remarks>
    /// <param name="accessor">The variation context accessor.</param>
    /// <param name="culture">The culture to read values for. Null means invariant.</param>
    /// <param name="segment">The segment to read values for. Null means the neutral segment.</param>
    public static IDisposable EnterVariationContext(
        this IVariationContextAccessor accessor,
        string? culture,
        string? segment = null)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        VariationContext? previous = accessor.VariationContext;
        accessor.VariationContext = new VariationContext(culture, segment);

        return new VariationContextScope(accessor, previous);
    }

    private sealed class VariationContextScope(IVariationContextAccessor accessor, VariationContext? previous)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            accessor.VariationContext = previous;
        }
    }
}
