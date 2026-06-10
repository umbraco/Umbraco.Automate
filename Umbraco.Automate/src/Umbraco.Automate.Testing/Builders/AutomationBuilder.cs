using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Testing.Builders;

/// <summary>
/// Fluent builder for <see cref="Automation"/> test instances.
/// </summary>
public class AutomationBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _alias = $"test-automation-{Guid.NewGuid():N}";
    private string _name = "Test Automation";
    private string? _description;
    private AutomationStatus _status = AutomationStatus.Published;
    private int? _publishedVersion = 1;
    private int _version = 1;
    private Guid _workspaceId = Guid.NewGuid();
    private Guid? _groupId;
    private DateTime _dateCreated = DateTime.UtcNow;
    private DateTime _dateModified = DateTime.UtcNow;
    private Guid? _createdByUserId;
    private Guid? _modifiedByUserId;
    private string? _canvasState;
    private TriggerConfiguration? _trigger;
    private IList<StepConfiguration> _steps = [];
    private IList<StepConnection> _connections = [];

    public AutomationBuilder WithId(Guid id) { _id = id; return this; }
    public AutomationBuilder WithAlias(string alias) { _alias = alias; return this; }
    public AutomationBuilder WithName(string name) { _name = name; return this; }
    public AutomationBuilder WithDescription(string? description) { _description = description; return this; }
    public AutomationBuilder WithStatus(AutomationStatus status) { _status = status; return this; }
    public AutomationBuilder WithVersion(int version) { _version = version; return this; }
    public AutomationBuilder WithPublishedVersion(int? publishedVersion) { _publishedVersion = publishedVersion; return this; }
    public AutomationBuilder WithWorkspaceId(Guid workspaceId) { _workspaceId = workspaceId; return this; }
    public AutomationBuilder WithGroupId(Guid? groupId) { _groupId = groupId; return this; }
    public AutomationBuilder WithDateCreated(DateTime dateCreated) { _dateCreated = dateCreated; return this; }
    public AutomationBuilder WithDateModified(DateTime dateModified) { _dateModified = dateModified; return this; }
    public AutomationBuilder WithCreatedByUserId(Guid? userId) { _createdByUserId = userId; return this; }
    public AutomationBuilder WithModifiedByUserId(Guid? userId) { _modifiedByUserId = userId; return this; }
    public AutomationBuilder WithCanvasState(string? canvasState) { _canvasState = canvasState; return this; }
    public AutomationBuilder WithTrigger(string triggerAlias, Dictionary<string, object?>? settings = null)
    {
        _trigger = new TriggerConfiguration
        {
            TriggerAlias = triggerAlias,
            Settings = settings ?? [],
        };
        return this;
    }

    public AutomationBuilder WithManualTrigger()
        => WithTrigger("umbracoAutomate.manual");

    public AutomationBuilder WithWebhookTrigger(string? authenticatorAlias = null, object? authenticatorSettings = null)
    {
        var settings = new Dictionary<string, object?>();

        if (authenticatorAlias is not null || authenticatorSettings is not null)
        {
            settings["authenticator"] = new Dictionary<string, object?>
            {
                ["alias"] = authenticatorAlias ?? "plain-secret",
                ["settings"] = ToDictionary(authenticatorSettings),
            };
        }

        return WithTrigger("umbracoAutomate.webhook", settings);
    }

    private static Dictionary<string, object?> ToDictionary(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is Dictionary<string, object?> dict)
        {
            return dict;
        }

        var json = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        });
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? [];
    }

    public AutomationBuilder AddStep(StepConfiguration step)
    {
        _steps.Add(step);
        return this;
    }

    public AutomationBuilder AddStep(StepConfigurationBuilder stepBuilder)
    {
        _steps.Add(stepBuilder.Build());
        return this;
    }

    public AutomationBuilder AddStep(string actionAlias, string? name = null, Dictionary<string, object?>? settings = null)
    {
        _steps.Add(new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = actionAlias,
            Name = name ?? actionAlias,
            Settings = settings ?? [],
        });
        return this;
    }

    public AutomationBuilder AddLogMessageStep(string message = "Test message", string logLevel = "Information")
        => AddStep("umbracoAutomate.logMessage", "Log Message", new Dictionary<string, object?>
        {
            ["message"] = message,
            ["logLevel"] = logLevel,
        });

    public AutomationBuilder WithConnection(Guid sourceStepId, Guid targetStepId, string? outcome = null, ConditionSet? filter = null)
    {
        _connections.Add(new StepConnection
        {
            SourceStepId = sourceStepId,
            TargetStepId = targetStepId,
            Outcome = outcome,
            Filter = filter,
        });
        return this;
    }

    /// <summary>
    /// Adds a connection from the trigger (<see cref="Guid.Empty"/>) to the specified step.
    /// </summary>
    public AutomationBuilder WithTriggerConnection(Guid targetStepId)
        => WithConnection(Guid.Empty, targetStepId);

    /// <summary>
    /// Configures the automation as a draft (unpublished).
    /// </summary>
    public AutomationBuilder AsDraft()
    {
        _status = AutomationStatus.Draft;
        _publishedVersion = null;
        return this;
    }

    /// <summary>
    /// Configures the automation as unpublished (after having been published).
    /// </summary>
    public AutomationBuilder AsUnpublished()
    {
        _status = AutomationStatus.Unpublished;
        return this;
    }

    public Automation Build() => new()
    {
        Id = _id,
        Alias = _alias,
        Name = _name,
        Description = _description,
        Status = _status,
        PublishedVersion = _publishedVersion,
        Version = _version,
        WorkspaceId = _workspaceId,
        GroupId = _groupId,
        DateCreated = _dateCreated,
        DateModified = _dateModified,
        CreatedByUserId = _createdByUserId,
        ModifiedByUserId = _modifiedByUserId,
        CanvasState = _canvasState,
        Trigger = _trigger,
        Steps = _steps,
        Connections = _connections,
    };

    public static implicit operator Automation(AutomationBuilder builder) => builder.Build();
}
