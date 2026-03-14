using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Infrastructure required by <see cref="TriggerBase{TSettings, TOutput}"/>.
/// Inherits from <see cref="StepTypeInfrastructure"/> with additional trigger-specific options.
/// </summary>
public sealed class TriggerInfrastructure : StepTypeInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerInfrastructure"/> class.
    /// </summary>
    public TriggerInfrastructure(IEditableModelResolver modelResolver, IOptions<DeduplicationOptions> deduplicationOptions)
        : base(modelResolver)
    {
        DeduplicationOptions = deduplicationOptions.Value;
    }

    internal DeduplicationOptions DeduplicationOptions { get; }
}
