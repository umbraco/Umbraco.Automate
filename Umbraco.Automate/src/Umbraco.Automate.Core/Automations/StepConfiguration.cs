namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// A configured step within an automation definition.
/// </summary>
public sealed class StepConfiguration
{
    /// <summary>
    /// Gets or sets the unique step identifier within this automation.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the alias of the action type (e.g. "httpRequest").
    /// </summary>
    public required string ActionAlias { get; set; }

    /// <summary>
    /// Gets or sets the user-defined label for this step.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the optional named connection ID for this step.
    /// </summary>
    public Guid? ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the step settings as a key-value dictionary.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];

    /// <summary>
    /// Gets or sets the input mappings — expression strings keyed by input property name.
    /// </summary>
    public Dictionary<string, string> InputMappings { get; set; } = [];

    /// <summary>
    /// Gets or sets the canvas position.
    /// </summary>
    public StepPosition Position { get; set; } = new();

    /// <summary>
    /// Gets or sets the error behavior for this step.
    /// </summary>
    public StepErrorBehavior ErrorBehavior { get; set; } = StepErrorBehavior.Retry;

    /// <summary>
    /// Gets or sets the retry interval when <see cref="ErrorBehavior"/> is <see cref="StepErrorBehavior.Retry"/>.
    /// </summary>
    public TimeSpan? RetryInterval { get; set; }

    /// <summary>
    /// Gets or sets the maximum retry count when <see cref="ErrorBehavior"/> is <see cref="StepErrorBehavior.Retry"/>.
    /// </summary>
    public int? MaxRetries { get; set; }
}

/// <summary>
/// A position on the visual canvas.
/// </summary>
public sealed class StepPosition
{
    /// <summary>
    /// Gets or sets the X coordinate.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate.
    /// </summary>
    public double Y { get; set; }
}
