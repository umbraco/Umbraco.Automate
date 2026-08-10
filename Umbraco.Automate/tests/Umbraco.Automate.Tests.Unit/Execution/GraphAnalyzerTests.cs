using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class GraphAnalyzerTests
{
    // Helper to create connections concisely.
    private static StepConnection Conn(Guid source, Guid target, string? outcome = null) =>
        new() { SourceStepId = source, TargetStepId = target, Outcome = outcome };

    // Connection leaving a named source handle — how the canvas saves container body/done edges.
    private static StepConnection Handled(Guid source, Guid target, string sourceHandle) =>
        new() { SourceStepId = source, TargetStepId = target, SourceHandle = sourceHandle };

    [Fact]
    public void Analyze_NoContainers_ReturnsEmpty()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var connections = new List<StepConnection> { Conn(a, b) };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid>());

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_Diamond_IdentifiesChildrenAndConvergence()
    {
        // ForEach → A (branch 1)
        //         → B (branch 2)
        // A → Merge
        // B → Merge
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a, "branch1"),
            Conn(forEach, b, "branch2"),
            Conn(a, merge),
            Conn(b, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        result.ShouldContainKey(forEach);
        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_TerminalBranches_NoConvergence()
    {
        // ForEach → A
        //         → B
        // (no merge — both branches are terminal)
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a, "branch1"),
            Conn(forEach, b, "branch2"),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBeNull();
    }

    [Fact]
    public void Analyze_SingleBranch_AllChildrenIncluded()
    {
        // ForEach → A → B → C → Merge
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a),
        // Single branch — no convergence possible with one branch
            Conn(a, b),
            Conn(b, c),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b, c }, ignoreOrder: true);
        // BranchEntry is only the direct successor — body is reached via outcome routing.
        scope.BranchEntryStepIds.ShouldBe(new HashSet<Guid> { a }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBeNull();
    }

    [Fact]
    public void Analyze_BranchEntryStepIds_OnlyImmediateSuccessors()
    {
        // ForEach → A1 (branch1) → A2 → Merge
        //         → B1 (branch2)      → Merge
        // BranchEntry must contain only the immediate successors (A1, B1), not A2.
        var forEach = Guid.NewGuid();
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a1, "branch1"),
            Conn(forEach, b1, "branch2"),
            Conn(a1, a2),
            Conn(a2, merge),
            Conn(b1, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BranchEntryStepIds.ShouldBe(new HashSet<Guid> { a1, b1 }, ignoreOrder: true);
        scope.BranchEntryStepIds.ShouldNotContain(a2);
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a1, a2, b1 }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_ThreeBranches_ConvergesCorrectly()
    {
        // Switch → A (case1)
        //        → B (case2)
        //        → C (default)
        // A → Merge
        // B → Merge
        // C → Merge
        var switchStep = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(switchStep, a, "case1"),
            Conn(switchStep, b, "case2"),
            Conn(switchStep, c, "default"),
            Conn(a, merge),
            Conn(b, merge),
            Conn(c, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { switchStep });

        var scope = result[switchStep];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b, c }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_AsymmetricBranches_ConvergesCorrectly()
    {
        // ForEach → A → A2 → Merge
        //         → B → Merge
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a, "branch1"),
            Conn(forEach, b, "branch2"),
            Conn(a, a2),
            Conn(a2, merge),
            Conn(b, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, a2, b }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_StepsAfterConvergence_NotIncludedAsChildren()
    {
        // ForEach → A → Merge → Final
        //         → B → Merge
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var merge = Guid.NewGuid();
        var final = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a, "branch1"),
            Conn(forEach, b, "branch2"),
            Conn(a, merge),
            Conn(b, merge),
            Conn(merge, final),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
        scope.BodyMemberStepIds.ShouldNotContain(merge);
        scope.BodyMemberStepIds.ShouldNotContain(final);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_ContainerWithNoOutgoing_ReturnsEmptyScope()
    {
        var forEach = Guid.NewGuid();

        var connections = new List<StepConnection>();

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.BodyMemberStepIds.ShouldBeEmpty();
        scope.ConvergenceStepId.ShouldBeNull();
    }

    [Fact]
    public void Analyze_EmptyBranch_SingleOutgoing()
    {
        // ForEach → Merge (single outgoing, treated as single branch body)
        var forEach = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        // Single branch — merge is the only step, and it's a child (no convergence with 1 branch)
        scope.BodyMemberStepIds.ShouldContain(merge);
        scope.ConvergenceStepId.ShouldBeNull();
    }

    [Fact]
    public void Analyze_DoneHandle_SetsConvergenceAndExcludesTargetFromBody()
    {
        // While ──body──→ A → B
        //       ──done──→ After
        var whileStep = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var after = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Handled(whileStep, a, "body"),
            Handled(whileStep, after, "done"),
            Conn(a, b),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { whileStep });

        var scope = result[whileStep];
        scope.ConvergenceStepId.ShouldBe(after);
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
        // The done target must not be spawned as a branch — it runs once, after the loop.
        scope.BranchEntryStepIds.ShouldBe(new HashSet<Guid> { a }, ignoreOrder: true);
    }

    [Fact]
    public void Analyze_DoneHandle_SingleBodyChain_DoesNotSwallowPostLoopStep()
    {
        // The regression this feature exists for: before the done handle, a chain drawn after a
        // While had no way to say "I am outside the loop" and was folded into the body.
        var whileStep = Guid.NewGuid();
        var body = Guid.NewGuid();
        var after = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Handled(whileStep, body, "body"),
            Handled(whileStep, after, "done"),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { whileStep });

        var scope = result[whileStep];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { body }, ignoreOrder: true);
        scope.BodyMemberStepIds.ShouldNotContain(after);
        scope.ConvergenceStepId.ShouldBe(after);
    }

    [Fact]
    public void Analyze_DoneHandle_WinsOverInferredConvergence()
    {
        // Both a done edge and a mergeable diamond are present. The explicit handle wins, so the
        // inferred merge stays inside the body where the user drew it.
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var merge = Guid.NewGuid();
        var after = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Handled(forEach, a, "body"),
            Handled(forEach, b, "body"),
            Handled(forEach, after, "done"),
            Conn(a, merge),
            Conn(b, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.ConvergenceStepId.ShouldBe(after);
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b, merge }, ignoreOrder: true);
    }

    [Fact]
    public void Analyze_NoDoneHandle_KeepsInferredConvergence()
    {
        // Back-compat: connections saved before the handles existed carry no source handle, so the
        // diamond must still resolve to the same convergence point it did before.
        var forEach = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var merge = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Conn(forEach, a),
            Conn(forEach, b),
            Conn(a, merge),
            Conn(b, merge),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.ConvergenceStepId.ShouldBe(merge);
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
    }

    [Fact]
    public void Analyze_BodyHandleOnly_NoConvergence()
    {
        // A container whose body is wired but whose done handle is left empty terminates the
        // workflow after the loop, exactly as an unhandled single chain does today.
        var whileStep = Guid.NewGuid();
        var a = Guid.NewGuid();

        var connections = new List<StepConnection>
        {
            Handled(whileStep, a, "body"),
        };

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { whileStep });

        var scope = result[whileStep];
        scope.BodyMemberStepIds.ShouldBe(new HashSet<Guid> { a }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBeNull();
    }
}
