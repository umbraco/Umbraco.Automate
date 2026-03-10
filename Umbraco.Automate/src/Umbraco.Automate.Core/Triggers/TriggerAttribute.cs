namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Marks a class as an automation trigger and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TriggerAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the trigger (e.g. "contentPublished").</param>
    /// <param name="name">A human-readable display name (e.g. "Content Published").</param>
    public TriggerAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias for this trigger.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a description of what this trigger does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category group for UI organisation (e.g. "Content", "Media").
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Gets or sets the Umbraco icon alias (e.g. "icon-globe").
    /// </summary>
    public string? Icon { get; set; }
}
