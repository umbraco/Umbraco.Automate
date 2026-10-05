// S1 — Declare fixed outcomes on an action: declaration rules (docs/plans/action-outcomes/STORIES.md)
//
// Pending: StepOutcomeValidator doesn't exist yet. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class StepOutcomeValidatorTests
{
    #region Given a valid declaration

    [Fact(Skip = "Pending: T4")]
    public void Validate_UniqueKeysAndOneDefault_Passes()
    {
        // Given [yes, no(default)] — Then validation passes.
    }

    [Fact(Skip = "Pending: T4")]
    public void Validate_EmptyDeclaration_Passes()
    {
        // Given no outcomes — Then validation passes.
    }

    [Fact(Skip = "Pending: T4")]
    public void Validate_NoDefault_Passes()
    {
        // Given [true, false] with no default — Then validation passes (a default is optional).
    }

    #endregion

    #region Sad path: broken declarations

    [Fact(Skip = "Pending: T4")]
    public void Validate_DuplicateKey_FailsNamingTheKey()
    {
        // Given two outcomes keyed "yes" — Then the error names "yes".
    }

    [Fact(Skip = "Pending: T4")]
    public void Validate_EmptyKey_Fails()
    {
        // Given an outcome with an empty key — Then validation fails for the empty key.
    }

    [Fact(Skip = "Pending: T4")]
    public void Validate_ReservedPrefix_FailsSayingKeysCannotStartWithDoubleUnderscore()
    {
        // Given an outcome keyed "__any__" — Then the error says keys can't start with "__".
    }

    [Fact(Skip = "Pending: T4")]
    public void Validate_TwoDefaults_FailsSayingAtMostOneDefaultIsAllowed()
    {
        // Given [yes(default), no(default)] — Then the error says at most one default is allowed.
    }

    #endregion
}
