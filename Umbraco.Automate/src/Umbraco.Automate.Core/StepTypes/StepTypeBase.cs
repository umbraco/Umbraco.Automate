using System.Reflection;
using Json.Schema;
using Json.Schema.Generation;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Abstract base class implementing <see cref="IStepType"/>. Reads metadata from a
/// <see cref="StepTypeAttribute"/>, auto-derives settings schema and output JSON Schema via reflection.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TOutput">The output POCO type (use <see cref="object"/> if no output).</typeparam>
/// <typeparam name="TAttribute">The attribute type deriving from <see cref="StepTypeAttribute"/> that provides metadata for this step type.</typeparam>
/// <typeparam name="TInfrastructure">The infrastructure type providing access to shared services and concerns for this step type.</typeparam>
public abstract class StepTypeBase<TSettings, TOutput, TAttribute, TInfrastructure> : IStepType
    where TSettings : class, new()
    where TOutput : class
    where TAttribute : StepTypeAttribute
    where TInfrastructure : StepTypeInfrastructure
{
    private readonly StepTypeAttribute _attribute;
    private readonly TInfrastructure _infrastructure;

    /// <summary>
    /// Gets the infrastructure instance providing access to shared services and concerns for this step type.
    /// </summary>
    public TInfrastructure Infrastructure => _infrastructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepTypeBase{TSettings, TOutput, TAttribute, TInfrastructure}"/> class.
    /// </summary>
    protected StepTypeBase(TInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
        _attribute = GetType().GetCustomAttribute<StepTypeAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Step type '{GetType().FullName}' is missing a required attribute deriving from [StepTypeAttribute].");
    }

    /// <inheritdoc />
    public string Alias => _attribute.Alias;

    /// <inheritdoc />
    public string Name => _attribute.Name;

    /// <inheritdoc />
    public virtual string? Description => _attribute.Description;

    /// <inheritdoc />
    public virtual string? Group => _attribute.Group;

    /// <inheritdoc />
    public virtual string? Icon => _attribute.Icon;

    /// <inheritdoc />
    public virtual string? ConnectionTypeAlias => _attribute.ConnectionTypeAlias;

    /// <inheritdoc />
    public Type? SettingsType => typeof(TSettings) == typeof(object) ? null : typeof(TSettings);

    /// <inheritdoc />
    public Type? OutputType => typeof(TOutput) == typeof(object) ? null : typeof(TOutput);

    /// <inheritdoc />
    public EditableModelSchema? GetSettingsSchema()
    {
        if (SettingsType is null)
        {
            return null;
        }

        return EditableModelSchemaBuilder.Build(SettingsType);
    }

    /// <inheritdoc />
    public JsonSchema? GetOutputSchema()
    {
        if (OutputType is null)
        {
            return null;
        }

        var config = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase,
        };

        return new JsonSchemaBuilder().FromType(OutputType, config).Build();
    }

    /// <inheritdoc />
    public virtual bool HasDynamicOutputSchema => false;

    /// <summary>
    /// Override to provide a dynamic output schema based on the step's typed settings.
    /// The default implementation returns the static CLR-based schema from <see cref="GetOutputSchema"/>.
    /// </summary>
    /// <param name="settings">The resolved typed settings, or null if unconfigured.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON Schema describing the output, or null if no output is produced.</returns>
    protected virtual Task<JsonSchema?> GetOutputSchemaAsync(TSettings? settings, CancellationToken cancellationToken = default)
        => Task.FromResult(GetOutputSchema());

    /// <inheritdoc />
    Task<JsonSchema?> IStepType.GetOutputSchemaAsync(Dictionary<string, object?>? settings, CancellationToken cancellationToken)
    {
        var typed = settings is { Count: > 0 } ? ResolveSettings(settings) : null;
        return GetOutputSchemaAsync(typed, cancellationToken);
    }

    /// <summary>
    /// Resolves settings from a raw dictionary to a typed <typeparamref name="TSettings"/> instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the step configuration.</param>
    /// <returns>The resolved settings, or null if settings are empty or the step type has no settings type.</returns>
    public TSettings? ResolveSettings(Dictionary<string, object?> settings)
        => _infrastructure.ModelResolver.ResolveModel<TSettings>(Alias, settings, GetSettingsSchema());

    /// <inheritdoc />
    object? IStepType.ResolveSettings(Dictionary<string, object?> settings)
        => ResolveSettings(settings);
}
