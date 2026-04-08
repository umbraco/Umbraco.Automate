using Json.Schema;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Optional interface for step types whose output schema depends on runtime configuration.
/// Implement this alongside <see cref="IStepType"/> when the output shape varies based on
/// the step's configured settings (e.g. an AI agent action whose output schema is user-defined).
/// </summary>
/// <remarks>
/// <para>
/// When a step type implements this interface, the catalogue API exposes
/// <c>hasDynamicOutputSchema: true</c> so the frontend knows to resolve the schema
/// via a dedicated endpoint rather than relying on the static catalogue schema.
/// </para>
/// <para>
/// Step types extending <see cref="StepTypeBase{TSettings,TOutput,TAttribute,TInfrastructure}"/>
/// should override the typed <c>GetOutputSchemaAsync(TSettings?, CancellationToken)</c> virtual
/// method and use the <c>ResolveOutputSchemaAsync</c> helper for the explicit interface bridge.
/// </para>
/// </remarks>
public interface IDynamicOutputSchemaProvider
{
    /// <summary>
    /// Resolves the output JSON Schema based on the step's current settings.
    /// </summary>
    /// <param name="settings">The step's configured settings dictionary, or null if unconfigured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A JSON Schema describing the output data structure, or null if the output schema
    /// cannot be determined from the given settings (e.g. required setting not yet configured).
    /// </returns>
    Task<JsonSchema?> GetOutputSchemaAsync(
        Dictionary<string, object?>? settings,
        CancellationToken cancellationToken = default);
}
