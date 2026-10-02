// S9 — Content and media actions offer named exits (docs/plans/action-outcomes/STORIES.md)
//
// Pending: no built-in action declares outcomes yet. Get Content lands in T8 (slice 1), content
// actions in T18 and media actions in T19. Turn each InlineData row on as its task lands. The
// builder fills in the body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class BuiltInActionOutcomeTests
{
    #region Given each built-in action that returns outcomes today

    [Theory(Skip = "Pending: T8 / T18 / T19")]
    [InlineData("GetContentAction")]
    [InlineData("GetContentPropertyAction")]
    [InlineData("FindContentAction")]
    [InlineData("CreateContentAction")]
    [InlineData("UpdateContentPropertyAction")]
    [InlineData("NotifyEditorAction")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_DefaultIsSuccess(string actionType)
    {
        // Then the single default outcome's key is "success".
        _ = actionType;
    }

    [Theory(Skip = "Pending: T8 / T18 / T19")]
    [InlineData("GetContentAction")]
    [InlineData("GetContentPropertyAction")]
    [InlineData("FindContentAction")]
    [InlineData("CreateContentAction")]
    [InlineData("UpdateContentPropertyAction")]
    [InlineData("NotifyEditorAction")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_KeysAreSuccessThenExistingConstants(string actionType)
    {
        // Then the keys equal ["success", ...the action's public Outcome* constant values].
        _ = actionType;
    }

    [Theory(Skip = "Pending: T8 / T18 / T19")]
    [InlineData("GetContentAction")]
    [InlineData("GetContentPropertyAction")]
    [InlineData("FindContentAction")]
    [InlineData("CreateContentAction")]
    [InlineData("UpdateContentPropertyAction")]
    [InlineData("NotifyEditorAction")]
    [InlineData("GetMediaAction")]
    [InlineData("GetMediaPropertyAction")]
    [InlineData("FindMediaAction")]
    [InlineData("CreateMediaAction")]
    [InlineData("UpdateMediaPropertyAction")]
    public void GetOutcomes_BuiltInAction_AllLabelsAreLocalizationKeys(string actionType)
    {
        // Then every label starts with "#uaOutcomes_".
        _ = actionType;
    }

    #endregion

    #region Given Get Content and missing content

    [Fact(Skip = "Pending: T8")]
    public async Task Execute_GetContentMissing_ReturnsNotFoundKeyUnchanged()
    {
        // Then the returned outcome is "notFound" (GetContentAction.OutcomeNotFound).
        await Task.CompletedTask;
    }

    #endregion
}
