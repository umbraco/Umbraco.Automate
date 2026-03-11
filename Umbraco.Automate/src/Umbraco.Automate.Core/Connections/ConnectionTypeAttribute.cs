namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Marks a class as a connection type and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ConnectionTypeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTypeAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the connection type (e.g. "slack").</param>
    /// <param name="name">A human-readable display name (e.g. "Slack").</param>
    public ConnectionTypeAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias for this connection type.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a description of what this connection type provides.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category group for UI organisation (e.g. "Messaging", "Email").
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco icon alias (e.g. "icon-message").
    /// </summary>
    public string? Icon { get; set; }
}
