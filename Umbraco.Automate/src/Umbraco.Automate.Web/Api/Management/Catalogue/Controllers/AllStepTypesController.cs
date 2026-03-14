using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Gets all registered step types (actions, control flows, and triggers).
/// </summary>
[ApiVersion("1.0")]
public sealed class AllStepTypesController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlows;
    private readonly TriggerCollection _triggers;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AllStepTypesController"/> class.
    /// </summary>
    public AllStepTypesController(
        ActionCollection actions,
        ControlFlowCollection controlFlows,
        TriggerCollection triggers,
        IUmbracoMapper mapper)
    {
        _actions = actions;
        _controlFlows = controlFlows;
        _triggers = triggers;
        _mapper = mapper;
    }

    /// <summary>
    /// Gets all registered step types with their metadata, schemas, and type discriminator.
    /// Optionally filter by type.
    /// </summary>
    /// <param name="type">Optional filter: "action", "controlFlow", or "trigger".</param>
    [HttpGet("step-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<StepTypeItemResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<StepTypeItemResponseModel>> GetAllStepTypes([FromQuery] string? type = null)
    {
        IEnumerable<StepTypeItemResponseModel> result = type?.ToLowerInvariant() switch
        {
            "action" => _mapper.MapEnumerable<IAction, ActionItemResponseModel>(_actions),
            "controlflow" => _mapper.MapEnumerable<IControlFlow, ControlFlowItemResponseModel>(_controlFlows),
            "trigger" => _mapper.MapEnumerable<ITrigger, TriggerItemResponseModel>(_triggers),
            _ => _mapper.MapEnumerable<IAction, ActionItemResponseModel>(_actions)
                .Concat<StepTypeItemResponseModel>(_mapper.MapEnumerable<IControlFlow, ControlFlowItemResponseModel>(_controlFlows))
                .Concat(_mapper.MapEnumerable<ITrigger, TriggerItemResponseModel>(_triggers)),
        };

        return Ok(result);
    }
}
