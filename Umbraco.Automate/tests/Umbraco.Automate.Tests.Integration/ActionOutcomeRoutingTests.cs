// S2 — The run follows the exit the action picked (docs/plans/action-outcomes/STORIES.md)
// Also covers S4 AC8 (run time uses saved settings) and S6 AC5–AC6 ("Any result" lines).
//
// Pending: ActionStepBody doesn't handle declared outcomes yet. Model setup on ApprovalOutcomeTests
// (real WorkflowCore host, real compiler). Automation under test: yes/no step, "yes" → A,
// "no" → B. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Integration;

public class ActionOutcomeRoutingTests
{
    #region Given the action returns outcome "yes"

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionReturnsYes_RunsStepA()
    {
        // Then step A has a completed step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionReturnsYes_DoesNotRunStepB()
    {
        // Then step B has no step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionReturnsYes_RecordsBranchOutcomeYes()
    {
        // Then the yes/no step run's BranchOutcome is "yes".
        await Task.CompletedTask;
    }

    #endregion

    #region Given the action returns success with no outcome

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionReturnsNoOutcome_RunsDefaultStepB()
    {
        // Then step B (the "no" default exit) has a completed step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionReturnsNoOutcome_RecordsBranchOutcomeNo()
    {
        // Then the yes/no step run's BranchOutcome is "no".
        await Task.CompletedTask;
    }

    #endregion

    #region Given an action that declares nothing, with an unnamed line to C

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionDeclaringNothing_RunsStepC()
    {
        // Then step C has a completed step run.
        await Task.CompletedTask;
    }

    #endregion

    #region Given an unnamed ("Any result") line to C from the yes/no step

    [Fact(Skip = "Pending: T9")]
    public async Task Run_AnyResultLine_FiresOnNamedOutcome()
    {
        // When the action returns "yes" — Then step C runs.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_AnyResultLine_FiresOnDefaultOutcome()
    {
        // When the action returns no outcome — Then step C runs.
        await Task.CompletedTask;
    }

    #endregion

    #region Given a bound dynamic step published with only an "other" exit

    [Fact(Skip = "Pending: T9")]
    public async Task Run_BoundOptionsResolveToAB_StillTakesOther()
    {
        // When the binding resolves to [a, b] and the action returns no outcome — Then the BranchOutcome is "other".
        await Task.CompletedTask;
    }

    #endregion

    #region Sad path

    [Fact(Skip = "Pending: T9")]
    public async Task Run_UndeclaredOutcome_LogsWarning()
    {
        // When the action returns "maybe" — Then the step run log has
        // "Action returned outcome 'maybe', which it does not declare."
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_UndeclaredOutcome_StillRunsAnyResultLine()
    {
        // Given an unnamed line to C — When the action returns "maybe" — Then step C runs.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_UndeclaredOutcome_DoesNotRunNamedExits()
    {
        // When the action returns "maybe" — Then neither A nor B has a step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_BrokenDeclaration_FailsTheStep()
    {
        // Given an action declaring two defaults — Then the step run fails with an error naming
        // the action and the broken rule.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_BrokenDeclarationWithRetry_IsNotRetried()
    {
        // Given error behaviour Retry — Then the step has exactly one attempt.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_NoOutcomeAndNoDefault_FailsTheStep()
    {
        // Given an action declaring [true, false] with no default — When it returns success with
        // no outcome — Then the step fails with "Action '<alias>' must return one of its declared outcomes."
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_GetOutcomesAsyncThrows_FailsTheStepWithValidationCategory()
    {
        // Given a dynamic action whose GetOutcomesAsync throws — Then the step run fails with
        // error category Validation.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T9")]
    public async Task Run_ActionFails_RunsNeitherExit()
    {
        // When the action fails — Then neither A nor B has a step run.
        await Task.CompletedTask;
    }

    #endregion
}
