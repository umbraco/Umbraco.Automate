namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Marks a class as an automation action and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the action (e.g. "httpRequest").</param>
    /// <param name="name">A human-readable display name (e.g. "HTTP Request").</param>
    public ActionAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias for this action.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a description of what this action does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category group for UI organisation (e.g. "Core", "Content").
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco icon alias (e.g. "icon-message").
    /// </summary>
    public string? Icon { get; set; }
}
