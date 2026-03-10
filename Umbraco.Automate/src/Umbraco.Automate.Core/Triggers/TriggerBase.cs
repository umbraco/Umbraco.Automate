using System.Reflection;
using Umbraco.Automate.Core.Settings;

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
    private readonly TriggerAttribute _attribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerBase{TSettings, TOutput}"/> class.
    /// </summary>
    protected TriggerBase()
    {
        _attribute = GetType().GetCustomAttribute<TriggerAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Trigger '{GetType().FullName}' is missing required [Trigger] attribute.");
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
}
