using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.Tests.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="Connection"/> test instances.
/// </summary>
public class ConnectionBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _alias = $"test-connection-{Guid.NewGuid():N}";
    private string _name = "Test Connection";
    private string _type = "testType";
    private Dictionary<string, object?> _settings = [];
    private int _version = 1;
    private DateTime _dateCreated = DateTime.UtcNow;
    private DateTime _dateModified = DateTime.UtcNow;
    private Guid? _createdByUserId;
    private Guid? _modifiedByUserId;

    public ConnectionBuilder WithId(Guid id) { _id = id; return this; }
    public ConnectionBuilder WithAlias(string alias) { _alias = alias; return this; }
    public ConnectionBuilder WithName(string name) { _name = name; return this; }
    public ConnectionBuilder WithType(string type) { _type = type; return this; }
    public ConnectionBuilder WithSettings(Dictionary<string, object?> settings) { _settings = settings; return this; }
    public ConnectionBuilder WithVersion(int version) { _version = version; return this; }
    public ConnectionBuilder WithDateCreated(DateTime dateCreated) { _dateCreated = dateCreated; return this; }
    public ConnectionBuilder WithDateModified(DateTime dateModified) { _dateModified = dateModified; return this; }
    public ConnectionBuilder WithCreatedByUserId(Guid? userId) { _createdByUserId = userId; return this; }
    public ConnectionBuilder WithModifiedByUserId(Guid? userId) { _modifiedByUserId = userId; return this; }

    public Connection Build() => new()
    {
        Id = _id,
        Alias = _alias,
        Name = _name,
        Type = _type,
        Settings = _settings,
        Version = _version,
        DateCreated = _dateCreated,
        DateModified = _dateModified,
        CreatedByUserId = _createdByUserId,
        ModifiedByUserId = _modifiedByUserId,
    };

    public static implicit operator Connection(ConnectionBuilder builder) => builder.Build();
}
