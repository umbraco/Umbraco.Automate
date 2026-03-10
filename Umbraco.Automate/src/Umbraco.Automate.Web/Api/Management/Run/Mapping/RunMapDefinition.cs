using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Run.Mapping;

/// <summary>
/// Map definitions for Run models.
/// </summary>
public class RunMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<AutomationRun, AutomationRunResponseModel>((_, _) => new AutomationRunResponseModel(), MapToResponse);
        mapper.Define<StepRun, StepRunResponseModel>((_, _) => new StepRunResponseModel(), MapToStepRunResponse);
    }

    // Umbraco.Code.MapAll
    private static void MapToResponse(AutomationRun source, AutomationRunResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.AutomationId = source.AutomationId;
        target.AutomationVersion = source.AutomationVersion;
        target.Status = source.Status;
        target.StartedUtc = source.StartedUtc;
        target.CompletedUtc = source.CompletedUtc;
        target.InitiatedBy = source.InitiatedBy;
        target.CorrelationId = source.CorrelationId;
        target.Error = source.Error;
        target.StepRuns = context.MapEnumerable<StepRun, StepRunResponseModel>(source.StepRuns);
    }

    // Umbraco.Code.MapAll
    private static void MapToStepRunResponse(StepRun source, StepRunResponseModel target, MapperContext context)
    {
        target.Id = source.Id;
        target.StepId = source.StepId;
        target.ActionAlias = source.ActionAlias;
        target.Status = source.Status;
        target.StartedUtc = source.StartedUtc;
        target.CompletedUtc = source.CompletedUtc;
        target.Error = source.Error;
        target.RetryCount = source.RetryCount;
        target.DurationMs = source.Duration?.TotalMilliseconds;
    }
}
