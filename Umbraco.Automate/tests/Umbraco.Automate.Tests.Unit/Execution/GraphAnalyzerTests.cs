using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class GraphAnalyzerTests
{
    // Helper to create connections concisely.
    private static StepConnection Conn(Guid source, Guid target, string? outcome = null) =>
        new() { SourceStepId = source, TargetStepId = target, Outcome = outcome };

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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, b, c }, ignoreOrder: true);
        scope.ConvergenceStepId.ShouldBeNull();
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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, b, c }, ignoreOrder: true);
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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, a2, b }, ignoreOrder: true);
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
        scope.ChildStepIds.ShouldBe(new HashSet<Guid> { a, b }, ignoreOrder: true);
        scope.ChildStepIds.ShouldNotContain(merge);
        scope.ChildStepIds.ShouldNotContain(final);
        scope.ConvergenceStepId.ShouldBe(merge);
    }

    [Fact]
    public void Analyze_ContainerWithNoOutgoing_ReturnsEmptyScope()
    {
        var forEach = Guid.NewGuid();

        var connections = new List<StepConnection>();

        var result = GraphAnalyzer.Analyze(connections, new HashSet<Guid> { forEach });

        var scope = result[forEach];
        scope.ChildStepIds.ShouldBeEmpty();
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
        scope.ChildStepIds.ShouldContain(merge);
        scope.ConvergenceStepId.ShouldBeNull();
    }
}
