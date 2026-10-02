// S9 — Content and media actions offer named exits: routing (docs/plans/action-outcomes/STORIES.md)
//
// Pending: Get Content doesn't declare outcomes yet. Automation under test: Get Content with
// "success" → A and "notFound" → B. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Integration;

public class BuiltInActionOutcomeRoutingTests
{
    #region Given the content exists

    [Fact(Skip = "Pending: T8")]
    public async Task Run_ContentExists_RunsSuccessStepA()
    {
        // Then step A has a completed step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T8")]
    public async Task Run_ContentExists_DoesNotRunNotFoundStepB()
    {
        // Then step B has no step run.
        await Task.CompletedTask;
    }

    #endregion

    #region Given the content doesn't exist

    [Fact(Skip = "Pending: T8")]
    public async Task Run_ContentMissing_RunsNotFoundStepB()
    {
        // Then step B has a completed step run.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T8")]
    public async Task Run_ContentMissing_DoesNotRunSuccessStepA()
    {
        // Then step A has no step run.
        await Task.CompletedTask;
    }

    #endregion

    #region Sad path: automation saved before outcomes were declared

    [Fact(Skip = "Pending: T8")]
    public async Task Run_LegacyUnnamedLineAndContentMissing_StillRunsStepC()
    {
        // Given an unnamed line from Get Content to C — Then step C runs.
        await Task.CompletedTask;
    }

    #endregion
}
