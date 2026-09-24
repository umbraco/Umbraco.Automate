using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a relation is saved in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per saved relation.
/// </summary>
[Trigger("umbracoAutomate.relationSaved", "Relation Saved",
    Description = "Fires when a relation is saved.",
    Group = "Relations",
    Icon = "icon-trafic",
    RequiredSections = [UmbracoConstants.Applications.Content])]
public sealed class RelationSavedTrigger
    : NotificationTriggerBase<RelationSavedTriggerSettings, RelationSavedTriggerOutput, RelationSavedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationSavedTrigger"/> class.
    /// </summary>
    public RelationSavedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RelationSavedNotification notification)
    {
        foreach (var relation in notification.SavedEntities)
        {
            yield return new TriggerEvent<RelationSavedTriggerOutput>
            {
                TriggerAlias = Alias,
                InitiatorType = TriggerInitiatorType.System,
                IdempotencyKey = GenerateVersionlessIdempotencyKey(relation.Key, relation.UpdateDate),
                Output = new RelationSavedTriggerOutput
                {
                    Key = relation.Key,
                    RelationTypeKey = relation.RelationType.Key,
                    ChildId = relation.ChildId,
                    ParentId = relation.ParentId,
                    ChildObjectTypeKey = relation.ChildObjectType,
                    ParentObjectTypeKey = relation.ParentObjectType,
                    Comment = relation.Comment,
                    IsNew = relation.CreateDate == relation.UpdateDate,
                },
            };
        }
    }
}
