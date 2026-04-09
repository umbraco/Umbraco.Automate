using Json.Schema;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Base class for actions whose output schema depends on runtime configuration.
/// Use this instead of <see cref="ActionBase{TSettings}"/> when the output shape
/// is determined by the step's settings (e.g. an AI agent action where each agent
/// has its own user-defined output schema).
/// </summary>
/// <typeparam name="TSettings">The settings POCO type.</typeparam>
public abstract class DynamicOutputActionBase<TSettings> : ActionBase<TSettings, object>
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicOutputActionBase{TSettings}"/> class.
    /// </summary>
    protected DynamicOutputActionBase(ActionInfrastructure infrastructure) : base(infrastructure) { }

    /// <inheritdoc />
    public override bool HasDynamicOutputSchema => true;

    /// <summary>
    /// Resolves the output JSON Schema based on the step's typed settings.
    /// </summary>
    /// <param name="settings">The resolved typed settings, or null if unconfigured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A JSON Schema describing the output data structure, or null if the output schema
    /// cannot be determined from the given settings (e.g. required setting not yet configured).
    /// </returns>
    protected abstract override Task<JsonSchema?> GetOutputSchemaAsync(
        TSettings? settings,
        CancellationToken cancellationToken = default);
}
