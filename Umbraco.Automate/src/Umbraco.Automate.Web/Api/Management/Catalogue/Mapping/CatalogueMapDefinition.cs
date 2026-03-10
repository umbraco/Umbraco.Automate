using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;

/// <summary>
/// Map definitions for Catalogue models (actions and triggers).
/// </summary>
public class CatalogueMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<IAction, ActionItemResponseModel>((_, _) => new ActionItemResponseModel(), MapToActionItem);
        mapper.Define<ITrigger, TriggerItemResponseModel>((_, _) => new TriggerItemResponseModel(), MapToTriggerItem);
    }

    // Umbraco.Code.MapAll
    private static void MapToActionItem(IAction source, ActionItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
    }

    // Umbraco.Code.MapAll
    private static void MapToTriggerItem(ITrigger source, TriggerItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = source.GetSettingsSchema();
        target.OutputProperties = source.GetOutputProperties();
    }
}
