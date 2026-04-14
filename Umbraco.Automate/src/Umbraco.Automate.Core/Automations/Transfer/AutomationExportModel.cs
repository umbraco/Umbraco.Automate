namespace Umbraco.Automate.Core.Automations.Transfer;

/// <summary>
/// The root export model for a single automation, serialized as a <c>.automate.json</c> file.
/// </summary>
public sealed class AutomationExportModel
{
    /// <summary>
    /// Gets or sets the format version for forward/backward compatibility.
    /// </summary>
    public required string FormatVersion { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the export was created.
    /// </summary>
    public required DateTime ExportedAt { get; init; }

    /// <summary>
    /// Gets or sets metadata about the product that produced this export.
    /// </summary>
    public required ExportSourceModel ExportedFrom { get; init; }

    /// <summary>
    /// Gets or sets the automation definition.
    /// </summary>
    public required AutomationExportDefinition Automation { get; init; }

    /// <summary>
    /// Gets or sets the connection references used by steps in this automation.
    /// Contains metadata only (alias, type, name) — no credentials.
    /// </summary>
    public IList<ConnectionReferenceModel> ConnectionReferences { get; init; } = [];
}

/// <summary>
/// Metadata about the product that produced the export.
/// </summary>
public sealed class ExportSourceModel
{
    /// <summary>
    /// Gets the product name.
    /// </summary>
    public required string Product { get; init; }

    /// <summary>
    /// Gets the product version.
    /// </summary>
    public required string Version { get; init; }
}

/// <summary>
/// The portable automation definition, free of environment-specific identifiers.
/// </summary>
public sealed class AutomationExportDefinition
{
    /// <summary>
    /// Gets or sets the unique alias.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the trigger configuration (with sensitive fields stripped).
    /// </summary>
    public TriggerConfiguration? Trigger { get; init; }

    /// <summary>
    /// Gets or sets the step configurations (with sensitive fields stripped and connection aliases instead of IDs).
    /// </summary>
    public IList<ExportStepModel> Steps { get; init; } = [];

    /// <summary>
    /// Gets or sets the connections between steps.
    /// </summary>
    public IList<StepConnection> Connections { get; init; } = [];

    /// <summary>
    /// Gets or sets the serialized canvas state.
    /// </summary>
    public string? CanvasState { get; init; }

    /// <summary>
    /// Gets or sets notification settings. Included only when the caller opts in via the <c>include</c> parameter.
    /// </summary>
    public AutomationNotificationSettings? NotificationSettings { get; init; }
}

/// <summary>
/// A step configuration within the export format. Uses connection aliases instead of GUIDs.
/// </summary>
public sealed class ExportStepModel
{
    /// <summary>
    /// Gets or sets the step identifier, local to this export file.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets or sets the action alias.
    /// </summary>
    public required string ActionAlias { get; init; }

    /// <summary>
    /// Gets or sets the user-defined step name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the step alias for binding expressions.
    /// </summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Gets or sets the connection alias (resolved from the connection ID). Null if the step doesn't use a connection.
    /// </summary>
    public string? ConnectionAlias { get; init; }

    /// <summary>
    /// Gets or sets the step settings (sensitive fields stripped).
    /// </summary>
    public Dictionary<string, object?> Settings { get; init; } = [];

    /// <summary>
    /// Gets or sets the input mappings (binding expressions).
    /// </summary>
    public Dictionary<string, string> InputMappings { get; init; } = [];

    /// <summary>
    /// Gets or sets the canvas position.
    /// </summary>
    public required StepPosition Position { get; init; }

    /// <summary>
    /// Gets or sets the error behavior.
    /// </summary>
    public StepErrorBehavior ErrorBehavior { get; init; }

    /// <summary>
    /// Gets or sets the retry interval.
    /// </summary>
    public TimeSpan? RetryInterval { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of retries.
    /// </summary>
    public int? MaxRetries { get; init; }
}

/// <summary>
/// Metadata about a connection referenced by the automation. No credentials are included.
/// </summary>
public sealed class ConnectionReferenceModel
{
    /// <summary>
    /// Gets the connection alias.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the connection type alias.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the connection display name.
    /// </summary>
    public required string Name { get; init; }
}
