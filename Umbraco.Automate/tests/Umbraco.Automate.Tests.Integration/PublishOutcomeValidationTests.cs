// S5 — Stale lines are caught before publish (docs/plans/action-outcomes/STORIES.md)
//
// Pending: AddDanglingOutcomeErrors doesn't exist yet. Model setup on PublishValidationTests.
// The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Integration;

public class PublishOutcomeValidationTests
{
    #region Given a step "Decide" with a stale line from outcome "b"

    [Fact(Skip = "Pending: T5")]
    public async Task SaveDraft_StaleOutcomeLine_Succeeds()
    {
        // Then the draft save does not throw.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task SaveDraft_StaleOutcomeLine_KeepsTheConnection()
    {
        // Then the saved automation still has the connection from "b".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_StaleLineMovedToA_Succeeds()
    {
        // Given the line is moved to exit "a" — Then publish succeeds.
        await Task.CompletedTask;
    }

    #endregion

    #region Sad path: publish blocked

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_StaleOutcomeLine_FailsWithStepAndOutcomeMessage()
    {
        // Then the error is "Step 'Decide' has a connection from outcome 'b', which the step no
        // longer has. Reconnect or remove it."
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_TwoStaleLines_ReportsTwoErrors()
    {
        // Then the validation error list has two entries.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_EmptyDynamicListWithNamedLine_FailsWithStaleOutcomeError()
    {
        // Given a dynamic step resolving to no outcomes, with a line from "a" — Then publish fails
        // with the stale-outcome error for "a".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_GetOutcomesAsyncThrows_FailsWithCouldNotListOutcomesError()
    {
        // Given a dynamic step whose GetOutcomesAsync throws — Then the error is
        // "Step '<name>' could not list its outcomes: <message>".
        await Task.CompletedTask;
    }

    #endregion

    #region Lines this rule must not flag

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_SwitchCaseLine_IsNotFlagged()
    {
        // Given a Switch step with a line from a case — Then no stale-outcome error is raised.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T5")]
    public async Task Publish_UnnamedLineFromDeclaringStep_IsNotFlagged()
    {
        // Given an "Any result" line — Then no stale-outcome error is raised.
        await Task.CompletedTask;
    }

    #endregion
}
