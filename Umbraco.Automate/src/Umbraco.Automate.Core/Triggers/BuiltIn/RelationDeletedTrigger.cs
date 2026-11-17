using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a relation is deleted in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per deleted relation.
/// </summary>
/// <remarks>
/// Umbraco also raises <see cref="RelationDeletedNotification"/> (with
/// <see cref="RelationDeletedNotification.IsAutomatic"/> set) when saving content, media or
/// members removes reference-tracking relations. Those are skipped unless
/// <see cref="RelationDeletedTriggerSettings.IncludeAutomaticRelations"/> is enabled.
/// </remarks>
[Trigger(
    "umbracoAutomate.relationDeleted",
    "Relation Deleted",
    Description = "Fires when a relation is deleted.",
    Group = "Relations",
    Icon = "icon-delete",
    RequiredSections = [UmbracoConstants.Applications.Settings])]
public sealed class RelationDeletedTrigger
    : NotificationTriggerBase<RelationDeletedTriggerSettings, RelationDeletedTriggerOutput, RelationDeletedNotification>
{
    private readonly IIdKeyMap _idKeyMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationDeletedTrigger"/> class.
    /// </summary>
    public RelationDeletedTrigger(TriggerInfrastructure infrastructure, IIdKeyMap idKeyMap)
        : base(infrastructure)
    {
        _idKeyMap = idKeyMap;
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
                    ChildKey = RelationEntityKeyResolver.ResolveEntityKey(_idKeyMap, relation.ChildId, relation.ChildObjectType),
                    ParentId = relation.ParentId,
                    ParentKey = RelationEntityKeyResolver.ResolveEntityKey(_idKeyMap, relation.ParentId, relation.ParentObjectType),
                    ChildObjectTypeKey = relation.ChildObjectType,
                    ParentObjectTypeKey = relation.ParentObjectType,
                    Comment = relation.Comment,
                    IsAutomatic = notification.IsAutomatic,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(RelationDeletedTriggerOutput output, RelationDeletedTriggerSettings? settings)
        => !output.IsAutomatic || settings?.IncludeAutomaticRelations == true;
}
