using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Base class for actions that reads metadata from the <see cref="ActionAttribute"/>
/// and auto-derives settings schema.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ActionBase<TSettings> : IAction
    where TSettings : class, new()
{
    private readonly ActionAttribute _attribute;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings}"/> class.
    /// </summary>
    protected ActionBase()
    {
        _attribute = GetType().GetCustomAttribute<ActionAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Action '{GetType().FullName}' is missing required [Action] attribute.");
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

    /// <inheritdoc />
    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
