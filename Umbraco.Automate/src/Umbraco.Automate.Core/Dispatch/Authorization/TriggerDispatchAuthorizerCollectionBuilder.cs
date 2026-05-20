using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Builder for the trigger dispatch authoriser collection. Registration order is
/// observable as iteration order at dispatch — but since the first denial short-circuits
/// the rest, order only matters for which failure reason gets logged.
/// </summary>
public class TriggerDispatchAuthorizerCollectionBuilder
    : LazyCollectionBuilderBase<TriggerDispatchAuthorizerCollectionBuilder, TriggerDispatchAuthorizerCollection, ITriggerDispatchAuthorizer>
{
    /// <inheritdoc />
    protected override TriggerDispatchAuthorizerCollectionBuilder This => this;
}

/// <summary>
/// The collection of registered trigger dispatch authorisers.
/// </summary>
public sealed class TriggerDispatchAuthorizerCollection : BuilderCollectionBase<ITriggerDispatchAuthorizer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerDispatchAuthorizerCollection"/> class.
    /// </summary>
    public TriggerDispatchAuthorizerCollection(Func<IEnumerable<ITriggerDispatchAuthorizer>> items) : base(items) { }
}
