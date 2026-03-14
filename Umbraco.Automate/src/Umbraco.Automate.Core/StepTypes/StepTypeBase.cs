using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Abstract base class implementing <see cref="IStepType"/>. Reads metadata from a
/// <see cref="StepTypeAttribute"/> and auto-derives settings schema via <see cref="EditableModelSchemaBuilder"/>.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TAttribute">The attribute type deriving from <see cref="StepTypeAttribute"/> that provides metadata for this step type.</typeparam>
/// <typeparam name="TInfrastructure">The infrastructure type providing access to shared services and concerns for this step type.</typeparam>
public abstract class StepTypeBase<TSettings, TAttribute, TInfrastructure> : IStepType
    where TSettings : class, new()
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
    /// Initializes a new instance of the <see cref="StepTypeBase{TSettings, TAttribute, TInfrastructure}"/> class.
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
    public Type? SettingsType => typeof(TSettings) == typeof(object) ? null : typeof(TSettings);

    /// <inheritdoc />
    public EditableModelSchema? GetSettingsSchema()
    {
        if (SettingsType is null)
        {
            return null;
        }

        return EditableModelSchemaBuilder.Build(SettingsType);
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
