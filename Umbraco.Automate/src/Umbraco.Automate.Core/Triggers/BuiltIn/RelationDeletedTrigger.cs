using Umbraco.Cms.Core.Notifications;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a relation is deleted in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per deleted relation.
/// </summary>
[Trigger(
    "umbracoAutomate.relationDeleted",
    "Relation Deleted",
    Description = "Fires when a relation is deleted.",
    Group = "Relations",
    Icon = "icon-delete",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class RelationDeletedTrigger
    : NotificationTriggerBase<RelationDeletedTriggerSettings, RelationDeletedTriggerOutput,RelationDeletedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationDeletedTrigger"/> class.
    /// </summary>
    public RelationDeletedTrigger(TriggerInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RelationDeletedNotification notification)
    {
        foreach (var relation in notification.DeletedEntities)
        {
            yield return new TriggerEvent<RelationDeletedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                IdempotencyKey = GenerateVersionlessIdempotencyKey(relation.Key),
                Output = new RelationDeletedTriggerOutput
                {
                    Key = relation.Key,
                    RelationTypeKey = relation.RelationType.Key,
                    ChildId = relation.ChildId,
                    ParentId = relation.ParentId,
                    ChildObjectTypeKey = relation.ChildObjectType,
                    ParentObjectTypeKey = relation.ParentObjectType,
                    Comment = relation.Comment,
                },
            };
        }
    }
}
