using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Api.Common.Builders;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Resolves the output schema for a step type, optionally using the step's configured settings
/// for step types that support dynamic output schemas.
/// </summary>
[ApiVersion("1.0")]
public sealed class ResolveStepTypeOutputSchemaController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlows;
    private readonly TriggerCollection _triggers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolveStepTypeOutputSchemaController"/> class.
    /// </summary>
    public ResolveStepTypeOutputSchemaController(
        ActionCollection actions,
        ControlFlowCollection controlFlows,
        TriggerCollection triggers)
    {
        _actions = actions;
        _controlFlows = controlFlows;
        _triggers = triggers;
    }

    /// <summary>
    /// Resolves the output schema for the specified step type. If the step type implements
    /// dynamic output schema resolution, the provided settings are used to determine the schema.
    /// Otherwise, the static CLR-based schema is returned.
    /// </summary>
    /// <param name="alias">The step type alias.</param>
    /// <param name="requestModel">The step's current settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("step-types/{alias}/output-schema")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveOutputSchema(
        string alias,
        ResolveOutputSchemaRequestModel requestModel,
        CancellationToken cancellationToken = default)
    {
        IStepType? stepType = _actions.GetByAlias(alias)
            ?? (IStepType?)_controlFlows.GetByAlias(alias)
            ?? _triggers.GetByAlias(alias);

        if (stepType is null)
        {
            return NotFound(new ProblemDetailsBuilder()
                .WithTitle("Step type not found")
                .WithDetail($"No step type with alias '{alias}' is registered.")
                .Build());
        }

        var schema = stepType is IDynamicOutputSchemaProvider dynamicProvider
            ? await dynamicProvider.GetOutputSchemaAsync(requestModel.Settings, cancellationToken)
            : stepType.GetOutputSchema();

        return Ok(OutputSchemaSerializer.Serialize(schema));
    }
}
