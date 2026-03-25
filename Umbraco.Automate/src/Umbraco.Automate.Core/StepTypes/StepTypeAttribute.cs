namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Base attribute for step type discovery metadata (alias, name, description, group, icon).
/// Not used directly — each domain has its own thin attribute that inherits from this.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public abstract class StepTypeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StepTypeAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the step type.</param>
    /// <param name="name">A human-readable display name.</param>
    protected StepTypeAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias for this step type.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a description of what this step type does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category group for UI organisation.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco icon alias.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Gets or sets the connection type alias that this step type requires (e.g. "slack").
    /// When set, the UI will prompt the user to select a connection of this type.
    /// </summary>
    public string? ConnectionTypeAlias { get; set; }
}
