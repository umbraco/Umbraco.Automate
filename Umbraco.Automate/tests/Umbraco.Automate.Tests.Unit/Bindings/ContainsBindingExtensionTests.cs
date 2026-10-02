// S4 — Outcomes driven by step settings: binding helper (docs/plans/action-outcomes/STORIES.md)
//
// Pending: the ContainsBinding() extension doesn't exist yet. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class ContainsBindingExtensionTests
{
    #region Given a string holding a binding expression

    [Fact(Skip = "Pending: T2")]
    public void ContainsBinding_WholeBindingExpression_ReturnsTrue()
    {
        // Given "${ steps.x.output }" — Then ContainsBinding() is true.
    }

    [Fact(Skip = "Pending: T2")]
    public void ContainsBinding_BindingInsideTemplate_ReturnsTrue()
    {
        // Given "Hello ${ trigger.name }" — Then ContainsBinding() is true.
    }

    #endregion

    #region Given a string without a binding

    [Fact(Skip = "Pending: T2")]
    public void ContainsBinding_PlainText_ReturnsFalse()
    {
        // Given "plain text" — Then ContainsBinding() is false.
    }

    [Fact(Skip = "Pending: T2")]
    public void ContainsBinding_Null_ReturnsFalse()
    {
        // Given null — Then ContainsBinding() is false.
    }

    #endregion
}
