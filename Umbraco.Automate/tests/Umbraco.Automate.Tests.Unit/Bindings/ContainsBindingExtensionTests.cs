// S4 — Outcomes driven by step settings: binding helper (docs/plans/action-outcomes/STORIES.md)

using Umbraco.Automate.Extensions;

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class ContainsBindingExtensionTests
{
    #region Given a string holding a binding expression

    [Fact]
    public void ContainsBinding_WholeBindingExpression_ReturnsTrue()
    {
        var value = "${ steps.x.output }";

        value.ContainsBinding().ShouldBeTrue();
    }

    [Fact]
    public void ContainsBinding_BindingInsideTemplate_ReturnsTrue()
    {
        var value = "Hello ${ trigger.name }";

        value.ContainsBinding().ShouldBeTrue();
    }

    #endregion

    #region Given a string without a binding

    [Fact]
    public void ContainsBinding_PlainText_ReturnsFalse()
    {
        var value = "plain text";

        value.ContainsBinding().ShouldBeFalse();
    }

    [Fact]
    public void ContainsBinding_Null_ReturnsFalse()
    {
        string? value = null;

        value.ContainsBinding().ShouldBeFalse();
    }

    #endregion
}
