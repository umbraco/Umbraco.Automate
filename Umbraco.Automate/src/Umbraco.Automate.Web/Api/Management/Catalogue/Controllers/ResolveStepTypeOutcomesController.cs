using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;

/// <summary>
/// Resolves the outcomes for a step type, using the step's configured settings for step types
/// whose outcomes depend on them.
/// </summary>
[ApiVersion("1.0")]
public sealed class ResolveStepTypeOutcomesController : CatalogueControllerBase
{
    private readonly ActionCollection _actions;
    private readonly ControlFlowCollection _controlFlows;
    private readonly TriggerCollection _triggers;
    private readonly IUmbracoMapper _mapper;
    private readonly ILogger<ResolveStepTypeOutcomesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResolveStepTypeOutcomesController"/> class.
    /// </summary>
    public ResolveStepTypeOutcomesController(
        ActionCollection actions,
        ControlFlowCollection controlFlows,
        TriggerCollection triggers,
        IUmbracoMapper mapper,
        ILogger<ResolveStepTypeOutcomesController> logger)
    {
        _actions = actions;
        _controlFlows = controlFlows;
        _triggers = triggers;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the outcomes for the specified step type, in declaration order. Step types with
    /// dynamic outcomes use the provided settings; static step types return their fixed list.
    /// Triggers never have outcomes.
    /// </summary>
    /// <param name="alias">The step type alias.</param>
    /// <param name="requestModel">The step's current settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("step-types/{alias}/outcomes")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<StepOutcomeResponseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveOutcomes(
        string alias,
        ResolveOutcomesRequestModel requestModel,
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

        // Triggers start an automation rather than finish with a result, so they never have outcomes.
        if (stepType is ITrigger)
        {
            return Ok(new List<StepOutcomeResponseModel>());
        }

        IReadOnlyList<StepOutcome>? outcomes;

        if (!stepType.HasDynamicOutcomes)
        {
            // Static outcomes don't depend on settings, so settings are not resolved (and not validated).
            outcomes = stepType.GetOutcomes();
        }
        else
        {
            try
            {
                outcomes = await stepType.GetOutcomesAsync(requestModel.Settings, cancellationToken);
            }
            catch (SettingsResolutionException ex)
            {
                // Only rejected settings are the caller's fault. Other exceptions (bugs in the
                // action itself) are deliberately not caught.
                return InvalidSettings(ex);
            }
        }

        return Ok(StepOutcomeResponseMapping.MapOutcomes(
            outcome => _mapper.Map<StepOutcomeResponseModel>(outcome)!, _logger, alias, outcomes));
    }
}
