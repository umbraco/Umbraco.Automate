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
public abstract class TriggerBase<TSettings, TOutput> : StepTypeBase<TSettings, TriggerAttribute, TriggerInfrastructure>, ITrigger
    where TSettings : class, new()
    where TOutput : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerBase{TSettings, TOutput}"/> class.
    /// </summary>
    protected TriggerBase(TriggerInfrastructure infrastructure) : base(infrastructure) { }

    /// <inheritdoc />
    public Type? OutputType => typeof(TOutput) == typeof(object) ? null : typeof(TOutput);

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
