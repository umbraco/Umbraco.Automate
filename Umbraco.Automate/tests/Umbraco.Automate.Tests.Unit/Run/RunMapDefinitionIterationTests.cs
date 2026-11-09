using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Run;

public class RunMapDefinitionIterationTests
{
    private readonly IUmbracoMapper _mapper;

    public RunMapDefinitionIterationTests()
    {
        var definitions = new MapDefinitionCollection(() => [new RunMapDefinition()]);
        _mapper = new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    private StepRunResponseModel Map(StepRun stepRun)
        => _mapper.Map<StepRun, StepRunResponseModel>(stepRun)!;

    private static StepRun NewStepRun(int? iterationIndex, int? iterationTotal) => new()
    {
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.forEach",
        IterationIndex = iterationIndex,
        IterationTotal = iterationTotal,
    };

    [Fact]
    public void Map_StepRunWithIteration_ReturnsIndexAndTotal()
    {
        var result = Map(NewStepRun(2, 5));

        result.IterationIndex.ShouldBe(2);
        result.IterationTotal.ShouldBe(5);
    }

    [Fact]
    public void Map_StepRunWithoutIteration_ReturnsNulls()
    {
        var result = Map(NewStepRun(null, null));

        result.IterationIndex.ShouldBeNull();
        result.IterationTotal.ShouldBeNull();
    }
}
