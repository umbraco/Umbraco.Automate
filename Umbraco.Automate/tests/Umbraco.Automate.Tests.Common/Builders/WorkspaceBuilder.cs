using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Automate.Tests.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="Workspace"/> test instances.
/// </summary>
public class WorkspaceBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _alias = $"test-workspace-{Guid.NewGuid():N}";
    private string _name = "Test Workspace";
    private Guid _serviceAccountKey = Guid.NewGuid();
    private IList<Guid> _userGroups = [];
    private IList<Guid> _connections = [];
    private int _version = 1;
    private DateTime _dateCreated = DateTime.UtcNow;
    private DateTime _dateModified = DateTime.UtcNow;
    private Guid? _createdByUserId;
    private Guid? _modifiedByUserId;

    public WorkspaceBuilder WithId(Guid id) { _id = id; return this; }
    public WorkspaceBuilder WithAlias(string alias) { _alias = alias; return this; }
    public WorkspaceBuilder WithName(string name) { _name = name; return this; }
    public WorkspaceBuilder WithServiceAccountKey(Guid key) { _serviceAccountKey = key; return this; }
    public WorkspaceBuilder WithUserGroups(params Guid[] groupIds) { _userGroups = groupIds.ToList(); return this; }
    public WorkspaceBuilder WithAllowedConnections(params Guid[] connectionIds) { _connections = connectionIds.ToList(); return this; }
    public WorkspaceBuilder WithVersion(int version) { _version = version; return this; }
    public WorkspaceBuilder WithDateCreated(DateTime dateCreated) { _dateCreated = dateCreated; return this; }
    public WorkspaceBuilder WithDateModified(DateTime dateModified) { _dateModified = dateModified; return this; }
    public WorkspaceBuilder WithCreatedByUserId(Guid? userId) { _createdByUserId = userId; return this; }
    public WorkspaceBuilder WithModifiedByUserId(Guid? userId) { _modifiedByUserId = userId; return this; }

    public Workspace Build() => new()
    {
        Id = _id,
        Alias = _alias,
        Name = _name,
        ServiceAccountKey = _serviceAccountKey,
        UserGroups = _userGroups,
        AllowedConnections = _connections,
        Version = _version,
        DateCreated = _dateCreated,
        DateModified = _dateModified,
        CreatedByUserId = _createdByUserId,
        ModifiedByUserId = _modifiedByUserId,
    };

    public static implicit operator Workspace(WorkspaceBuilder builder) => builder.Build();
}
