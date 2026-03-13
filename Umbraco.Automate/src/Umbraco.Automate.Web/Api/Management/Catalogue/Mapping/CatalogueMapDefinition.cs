using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Settings;
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
        mapper.Define<IConnectionType, ConnectionTypeItemResponseModel>((_, _) => new ConnectionTypeItemResponseModel(), MapToConnectionTypeItem);
    }

    // Umbraco.Code.MapAll
    private static void MapToActionItem(IAction source, ActionItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = MapSchema(source.GetSettingsSchema());
    }

    // Umbraco.Code.MapAll
    private static void MapToTriggerItem(ITrigger source, TriggerItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = MapSchema(source.GetSettingsSchema());
        target.OutputProperties = source.GetOutputProperties()
            .Select(p => new TriggerOutputPropertyResponseModel
            {
                Name = p.Name,
                Type = p.Type.Name,
                Description = p.Description,
            })
            .ToList();
    }

    // Umbraco.Code.MapAll
    private static void MapToConnectionTypeItem(IConnectionType source, ConnectionTypeItemResponseModel target, MapperContext context)
    {
        target.Alias = source.Alias;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Group = source.Group;
        target.Icon = source.Icon;
        target.SettingsSchema = MapSchema(source.GetSettingsSchema());
    }

    private static EditableModelSchemaResponseModel? MapSchema(EditableModelSchema? schema)
    {
        if (schema is null)
            return null;

        return new EditableModelSchemaResponseModel
        {
            Fields = schema.Fields.Select(f => new EditableModelFieldDescriptorResponseModel
            {
                PropertyName = ToCamelCase(f.PropertyName),
                Label = f.Label,
                PropertyType = f.PropertyType.Name,
                Description = f.Description,
                EditorUiAlias = f.EditorUiAlias,
                EditorConfig = f.EditorConfig,
                SortOrder = f.SortOrder,
                IsSensitive = f.IsSensitive,
                Group = f.Group,
                IsRequired = f.ValidationRules.OfType<RequiredAttribute>().Any(),
            }).ToList(),
        };
    }

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
