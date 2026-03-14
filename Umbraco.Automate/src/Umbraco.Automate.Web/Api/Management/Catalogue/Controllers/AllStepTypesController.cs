using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered step types (actions and control flow).
/// </summary>
[ApiVersion("1.0")]
public sealed class AllStepTypesController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlow;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllStepTypesController"/> class.
    /// </summary>
    public AllStepTypesController(
        ActionCollection actions,
        ControlFlowCollection controlFlow,
        IUmbracoMapper mapper)
    {
        _actions = actions;
        _controlFlow = controlFlow;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered step types with their metadata, schemas, and type discriminator.
    /// </summary>
    [HttpGet("step-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<StepTypeItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<StepTypeItemResponseModel>> GetAllStepTypes()
    {
        var actionItems = _actions
            .Cast<IStepType>()
            .Select(s => MapToStepTypeItem(s, "action"));

        var controlFlowItems = _controlFlow
            .Cast<IStepType>()
            .Select(s => MapToStepTypeItem(s, "controlFlow"));

        return Ok(actionItems.Concat(controlFlowItems));
    }

    private static StepTypeItemResponseModel MapToStepTypeItem(IStepType source, string type) => new()
    {
        Alias = source.Alias,
        Name = source.Name,
        Description = source.Description,
        Group = source.Group,
        Icon = source.Icon,
        SettingsSchema = source.GetSettingsSchema(),
        Type = type,
    };
}
