using System.Reflection;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Base class for triggers that reads metadata from the <see cref="TriggerAttribute"/>
/// and auto-derives settings schema and output properties.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TOutput">The output POCO type (use <see cref="object"/> if no output).</typeparam>
public abstract class TriggerBase<TSettings, TOutput> : ITrigger
    where TSettings : class, new()
    where TOutput : class
{
    private readonly StepTypeAttribute _attribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerBase{TSettings, TOutput}"/> class.
    /// </summary>
    protected TriggerBase(TriggerInfrastructure infrastructure)
    {
        Infrastructure = infrastructure;
        _attribute = GetType().GetCustomAttribute<StepTypeAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Trigger '{GetType().FullName}' is missing required [Trigger] attribute.");
    }

    /// <summary>
    /// Gets the infrastructure envelope for accessing shared services.
    /// </summary>
    protected TriggerInfrastructure Infrastructure { get; }

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
    public IReadOnlyList<TriggerOutputProperty> GetOutputProperties()
    {
        if (OutputType is null)
        {
            return [];
        }

        return OutputType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new TriggerOutputProperty
            {
                Name = char.ToLowerInvariant(p.Name[0]) + p.Name[1..],
                Type = p.PropertyType,
                Description = null,
            })
            .ToList();
    }

    /// <summary>
    /// Resolves trigger settings from a raw dictionary to a typed <typeparamref name="TSettings"/> instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the trigger configuration.</param>
    /// <returns>The resolved settings, or null if settings are empty or the trigger has no settings type.</returns>
    public TSettings? ResolveSettings(Dictionary<string, object?> settings)
        => Infrastructure.ModelResolver.ResolveModel<TSettings>(Alias, settings, GetSettingsSchema());

    /// <inheritdoc />
    object? ITrigger.ResolveSettings(Dictionary<string, object?> settings)
        => ResolveSettings(settings);
}
