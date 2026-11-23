using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a relation is saved in Umbraco CMS.
/// Produces one <see cref="TriggerEvent"/> per saved relation.
/// </summary>
/// <remarks>
/// Umbraco also raises <see cref="RelationSavedNotification"/> (with
/// <see cref="RelationSavedNotification.IsAutomatic"/> set) when saving content, media or
/// members adds reference-tracking relations. Those are skipped unless
/// <see cref="RelationSavedTriggerSettings.IncludeAutomaticRelations"/> is enabled.
/// </remarks>
[Trigger("umbracoAutomate.relationSaved", "Relation Saved",
    Description = "Fires when a relation is saved.",
    Group = "Relations",
    Icon = "icon-trafic",
    RequiredSections = [UmbracoConstants.Applications.Settings])]
public sealed class RelationSavedTrigger
    : NotificationTriggerBase<RelationSavedTriggerSettings, RelationSavedTriggerOutput, RelationSavedNotification>
{
    private readonly IIdKeyMap _idKeyMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationSavedTrigger"/> class.
    /// </summary>
    public RelationSavedTrigger(TriggerInfrastructure infrastructure, IIdKeyMap idKeyMap)
        : base(infrastructure)
    {
        _idKeyMap = idKeyMap;
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
                // IRelation has no CMS VersionId — UpdateDate advances per save and is the
                // only stable distinguisher between legitimate sequential saves of the
                // same relation.
                IdempotencyKey = GenerateVersionlessIdempotencyKey(relation.Key, relation.UpdateDate),
                Output = new RelationSavedTriggerOutput
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
                    IsNew = relation.CreateDate == relation.UpdateDate,
                    IsAutomatic = notification.IsAutomatic,
                },
            };
        }
    }

    /// <inheritdoc />
    protected override bool CanHandle(RelationSavedTriggerOutput output, RelationSavedTriggerSettings? settings)
        => !output.IsAutomatic || settings?.IncludeAutomaticRelations == true;
}
