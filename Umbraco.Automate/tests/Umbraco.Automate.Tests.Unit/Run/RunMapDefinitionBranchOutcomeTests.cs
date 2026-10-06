// S10 — Exit taken on step runs (docs/plans/action-outcomes/STORIES.md), AC3.

using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Web.Api.Management.Run.Mapping;
using Umbraco.Automate.Web.Api.Management.Run.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Run;

public class RunMapDefinitionBranchOutcomeTests
{
    private readonly IUmbracoMapper _mapper;

    public RunMapDefinitionBranchOutcomeTests()
    {
        var definitions = new MapDefinitionCollection(() => [new RunMapDefinition()]);
        _mapper = new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    private StepRunResponseModel Map(StepRun stepRun)
        => _mapper.Map<StepRun, StepRunResponseModel>(stepRun)!;

    private static StepRun NewStepRun(string? branchOutcome) => new()
    {
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "test.action",
        BranchOutcome = branchOutcome,
    };

    [Fact]
    public void Map_StepRunWithBranchOutcome_ReturnsThatBranchOutcome()
    {
        var result = Map(NewStepRun("yes"));

        result.BranchOutcome.ShouldBe("yes");
    }

    [Fact]
    public void Map_StepRunWithoutBranchOutcome_ReturnsNull()
    {
        var result = Map(NewStepRun(null));

        result.BranchOutcome.ShouldBeNull();
    }
}
