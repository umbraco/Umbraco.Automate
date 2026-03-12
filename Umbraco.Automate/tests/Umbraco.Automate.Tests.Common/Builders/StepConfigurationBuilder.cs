using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Tests.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="StepConfiguration"/> test instances.
/// </summary>
public class StepConfigurationBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _actionAlias = "umbracoAutomate.logMessage";
    private string _name = "Test Step";
    private Dictionary<string, object?> _settings = [];
    private Dictionary<string, string> _inputMappings = [];
    private Guid? _connectionId;
    private StepErrorBehavior _errorBehavior = StepErrorBehavior.Terminate;

    public StepConfigurationBuilder WithId(Guid id) { _id = id; return this; }
    public StepConfigurationBuilder WithConnectionId(Guid? connectionId) { _connectionId = connectionId; return this; }
    public StepConfigurationBuilder WithActionAlias(string alias) { _actionAlias = alias; return this; }
    public StepConfigurationBuilder WithName(string name) { _name = name; return this; }
    public StepConfigurationBuilder WithErrorBehavior(StepErrorBehavior behavior) { _errorBehavior = behavior; return this; }

    public StepConfigurationBuilder WithSetting(string key, object? value)
    {
        _settings[key] = value;
        return this;
    }

    public StepConfigurationBuilder WithSettings(Dictionary<string, object?> settings)
    {
        _settings = settings;
        return this;
    }

    public StepConfigurationBuilder WithInputMapping(string key, string expression)
    {
        _inputMappings[key] = expression;
        return this;
    }

    /// <summary>
    /// Configures as a Log Message step with specified message and level.
    /// </summary>
    public StepConfigurationBuilder AsLogMessage(string message = "Test message", string logLevel = "Information")
    {
        _actionAlias = "umbracoAutomate.logMessage";
        _name = "Log Message";
        _settings = new Dictionary<string, object?>
        {
            ["message"] = message,
            ["logLevel"] = logLevel,
        };
        return this;
    }

    public StepConfiguration Build() => new()
    {
        Id = _id,
        ActionAlias = _actionAlias,
        Name = _name,
        ConnectionId = _connectionId,
        Settings = _settings,
        InputMappings = _inputMappings,
        ErrorBehavior = _errorBehavior,
    };

    public static implicit operator StepConfiguration(StepConfigurationBuilder builder) => builder.Build();
}
