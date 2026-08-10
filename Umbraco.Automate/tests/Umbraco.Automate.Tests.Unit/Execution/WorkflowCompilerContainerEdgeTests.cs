using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Covers which connections leaving a container become body branch edges. The step on a
/// container's done handle runs once after the container finishes — it is wired as the container's
/// outcome, so including it here would spawn it once per iteration as well.
/// </summary>
public class WorkflowCompilerContainerEdgeTests
{
    private static StepConnection Conn(Guid source, Guid target, string? sourceHandle = null) =>
        new() { SourceStepId = source, TargetStepId = target, SourceHandle = sourceHandle };

    [Fact]
    public void BuildContainerBranchEdges_ExcludesDoneEdge()
    {
        var container = Guid.NewGuid();
        var body = Guid.NewGuid();
        var after = Guid.NewGuid();

        var edges = WorkflowCompiler.BuildContainerBranchEdges(
            [Conn(container, body, "body"), Conn(container, after, "done")],
            new HashSet<Guid> { container });

        edges[container].Select(e => e.TargetStepId).ShouldBe([body]);
    }

    [Fact]
    public void BuildContainerBranchEdges_KeepsUnhandledEdges()
    {
        // Connections saved before the handles existed have no source handle and are body edges.
        var container = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var edges = WorkflowCompiler.BuildContainerBranchEdges(
            [Conn(container, a), Conn(container, b)],
            new HashSet<Guid> { container });

        edges[container].Select(e => e.TargetStepId).ShouldBe([a, b], ignoreOrder: true);
    }

    [Fact]
    public void BuildContainerBranchEdges_DoneHandleIsCaseInsensitive()
    {
        var container = Guid.NewGuid();
        var body = Guid.NewGuid();
        var after = Guid.NewGuid();

        var edges = WorkflowCompiler.BuildContainerBranchEdges(
            [Conn(container, body, "body"), Conn(container, after, "DONE")],
            new HashSet<Guid> { container });

        edges[container].Select(e => e.TargetStepId).ShouldBe([body]);
    }

    [Fact]
    public void BuildContainerBranchEdges_IgnoresNonContainerSources()
    {
        var container = Guid.NewGuid();
        var plainStep = Guid.NewGuid();
        var target = Guid.NewGuid();

        var edges = WorkflowCompiler.BuildContainerBranchEdges(
            [Conn(plainStep, target)],
            new HashSet<Guid> { container });

        edges.ShouldBeEmpty();
    }

    [Fact]
    public void BuildContainerBranchEdges_OnlyDoneEdge_LeavesContainerWithNoBranches()
    {
        var container = Guid.NewGuid();
        var after = Guid.NewGuid();

        var edges = WorkflowCompiler.BuildContainerBranchEdges(
            [Conn(container, after, "done")],
            new HashSet<Guid> { container });

        // No entry at all rather than an empty list — the container simply never branches.
        edges.ShouldBeEmpty();
    }
}
