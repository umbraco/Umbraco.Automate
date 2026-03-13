using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// Maps between <see cref="AutomationGroup"/> domain model and <see cref="AutomationGroupEntity"/> EF entity.
/// </summary>
internal sealed class AutomationGroupFactory
{
    public AutomationGroup BuildDomain(AutomationGroupEntity entity)
    {
        return new AutomationGroup
        {
            Id = entity.Id,
            Name = entity.Name,
            ParentId = entity.ParentId,
            WorkspaceId = entity.WorkspaceId,
            DateCreated = entity.DateCreated,
        };
    }

    public AutomationGroupEntity BuildEntity(AutomationGroup group)
    {
        return new AutomationGroupEntity
        {
            Id = group.Id,
            Name = group.Name,
            ParentId = group.ParentId,
            WorkspaceId = group.WorkspaceId,
            DateCreated = group.DateCreated,
        };
    }

    public void UpdateEntity(AutomationGroupEntity entity, AutomationGroup group)
    {
        entity.Name = group.Name;
        entity.ParentId = group.ParentId;
        // WorkspaceId and DateCreated intentionally not updated
    }
}
