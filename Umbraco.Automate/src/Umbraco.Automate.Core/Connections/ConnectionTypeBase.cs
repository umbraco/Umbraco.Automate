using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Base class for connection types that reads metadata from the <see cref="ConnectionTypeAttribute"/>
/// and auto-derives settings schema.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ConnectionTypeBase<TSettings> : IConnectionType
    where TSettings : class, new()
{
    private readonly ConnectionTypeAttribute _attribute;
    private readonly ConnectionTypeInfrastructure _infrastructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTypeBase{TSettings}"/> class.
    /// </summary>
    protected ConnectionTypeBase(ConnectionTypeInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
        _attribute = GetType().GetCustomAttribute<ConnectionTypeAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Connection type '{GetType().FullName}' is missing required [ConnectionType] attribute.");
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
    /// Resolves connection settings from a raw dictionary to a typed <typeparamref name="TSettings"/> instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the connection configuration.</param>
    /// <returns>The resolved settings, or null if settings are empty or the type has no settings.</returns>
    public TSettings? ResolveSettings(Dictionary<string, object?> settings)
        => _infrastructure.ModelResolver.ResolveModel<TSettings>(Alias, settings, GetSettingsSchema());

    /// <inheritdoc />
    object? IConnectionType.ResolveSettings(Dictionary<string, object?> settings)
        => ResolveSettings(settings);

    /// <summary>
    /// Validates that the connection settings are correct and the external system is reachable.
    /// Default implementation always returns true. Override to implement connectivity checks.
    /// </summary>
    public virtual Task<bool> ValidateAsync(object? settings, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
