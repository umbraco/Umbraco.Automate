using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Infrastructure infrastructure required by <see cref="TriggerBase{TSettings, TOutput}"/>.
/// Passed as a single envelope to avoid leaking infrastructure concerns into trigger implementations.
/// </summary>
public sealed class TriggerInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerInfrastructure"/> class.
    /// </summary>
    public TriggerInfrastructure(IEditableModelResolver modelResolver, IOptions<DeduplicationOptions> deduplicationOptions)
    {
        ModelResolver = modelResolver;
        DeduplicationOptions = deduplicationOptions.Value;
    }

    internal IEditableModelResolver ModelResolver { get; }

    internal DeduplicationOptions DeduplicationOptions { get; }
}
