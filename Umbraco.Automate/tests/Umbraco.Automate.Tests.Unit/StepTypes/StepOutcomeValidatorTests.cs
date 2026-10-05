// S1 — Declare fixed outcomes on an action: declaration rules (docs/plans/action-outcomes/STORIES.md)

using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class StepOutcomeValidatorTests
{
    private static StepOutcome Outcome(string key, bool isDefault = false) =>
        new(key, key) { IsDefault = isDefault };

    #region Given a valid declaration

    [Fact]
    public void Validate_UniqueKeysAndOneDefault_Passes()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("yes"), Outcome("no", isDefault: true)]);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_EmptyDeclaration_Passes()
    {
        var errors = StepOutcomeValidator.Validate([]);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NoDefault_Passes()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("true"), Outcome("false")]);

        errors.ShouldBeEmpty();
    }

    #endregion

    #region Sad path: broken declarations

    [Fact]
    public void Validate_DuplicateKey_FailsNamingTheKey()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("yes"), Outcome("yes")]);

        errors.ShouldHaveSingleItem().ShouldContain("'yes'");
    }

    [Fact]
    public void Validate_EmptyKey_Fails()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("yes"), Outcome("")]);

        errors.ShouldHaveSingleItem().ShouldBe("Outcome #2 has an empty key.");
    }

    [Fact]
    public void Validate_WhitespaceKey_Fails()
    {
        var errors = StepOutcomeValidator.Validate([Outcome(" ")]);

        errors.ShouldHaveSingleItem().ShouldBe("Outcome #1 has an empty key.");
    }

    [Fact]
    public void Validate_NullOutcome_Fails()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("yes"), null!]);

        errors.ShouldHaveSingleItem().ShouldBe("Outcome #2 is null.");
    }

    [Fact]
    public void Validate_ReservedPrefix_FailsSayingKeysCannotStartWithDoubleUnderscore()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("__any__")]);

        errors.ShouldHaveSingleItem().ShouldBe("Outcome keys can't start with '__': '__any__'.");
    }

    [Fact]
    public void Validate_TwoDefaults_FailsSayingAtMostOneDefaultIsAllowed()
    {
        var errors = StepOutcomeValidator.Validate([Outcome("yes", true), Outcome("no", true)]);

        errors.ShouldHaveSingleItem().ShouldBe("At most one outcome can be the default; found 2: 'yes', 'no'.");
    }

    #endregion
}
