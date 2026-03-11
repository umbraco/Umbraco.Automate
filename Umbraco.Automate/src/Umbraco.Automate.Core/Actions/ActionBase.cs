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
    private readonly ActionInfrastructure _infrastructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings}"/> class.
    /// </summary>
    protected ActionBase(ActionInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
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

    /// <summary>
    /// Resolves action settings from a raw dictionary to a typed <typeparamref name="TSettings"/> instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the step configuration.</param>
    /// <returns>The resolved settings, or null if settings are empty or the action has no settings type.</returns>
    public TSettings? ResolveSettings(Dictionary<string, object?> settings)
        => _infrastructure.ModelResolver.ResolveModel<TSettings>(Alias, settings, GetSettingsSchema());

    /// <inheritdoc />
    object? IAction.ResolveSettings(Dictionary<string, object?> settings)
        => ResolveSettings(settings);

    /// <inheritdoc />
    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
