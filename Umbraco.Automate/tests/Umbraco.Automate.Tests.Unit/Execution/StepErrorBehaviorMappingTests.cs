using Shouldly;
using Umbraco.Automate.Core.Automations;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Guards against divergence between <see cref="StepErrorBehavior"/> and
/// <see cref="WorkflowErrorHandling"/>. WorkflowCompiler casts directly between the two
/// when applying error handling to a WorkflowCore step, so the numeric values must match.
/// </summary>
public class StepErrorBehaviorMappingTests
{
    [Theory]
    [InlineData(StepErrorBehavior.Retry, WorkflowErrorHandling.Retry)]
    [InlineData(StepErrorBehavior.Suspend, WorkflowErrorHandling.Suspend)]
    [InlineData(StepErrorBehavior.Terminate, WorkflowErrorHandling.Terminate)]
    [InlineData(StepErrorBehavior.Compensate, WorkflowErrorHandling.Compensate)]
    public void EnumValuesMatch(StepErrorBehavior ours, WorkflowErrorHandling theirs)
        => ((int)ours).ShouldBe((int)theirs);
}
