// S4 — Outcomes driven by step settings: resolve endpoint (docs/plans/action-outcomes/STORIES.md)
// Also covers S7 AC3 (labels returned raw).
//
// Pending: ResolveStepTypeOutcomesController doesn't exist yet. Follow
// ResolveStepTypeOutputSchemaControllerTests for setup. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class ResolveStepTypeOutcomesControllerTests
{
    #region Given a dynamic action and options a, b

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_ReturnsOk()
    {
        // Then the result is 200.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_ReturnsAThenBThenOther()
    {
        // Then the keys are [a, b, other], in order.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_MarksOtherAsDefault()
    {
        // Then only "other" has isDefault = true.
        await Task.CompletedTask;
    }

    #endregion

    #region Given a static yes/no action

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_StaticAction_ReturnsItsStaticList()
    {
        // Given any settings — Then the keys are [yes, no].
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_ActionDeclaringNothing_ReturnsEmptyList()
    {
        // Then the response is an empty array, not null.
        await Task.CompletedTask;
    }

    #endregion

    #region Given outcome labels that are a #key and literal text

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_KeyLabel_IsReturnedUntranslated()
    {
        // Given label "#uaOutcomes_found" — Then the response label is "#uaOutcomes_found".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_LiteralLabel_IsReturnedAsIs()
    {
        // Given label "Breaking news" — Then the response label is "Breaking news".
        await Task.CompletedTask;
    }

    #endregion

    #region Sad path

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_UnknownAlias_ReturnsNotFound()
    {
        // Given alias "nope" — Then the result is 404 "Step type not found".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_InvalidSettings_ReturnsBadRequest()
    {
        // Given settings ResolveSettings rejects — Then the result is 400.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_InvalidSettings_ProblemTitleIsInvalidSettings()
    {
        // Then the ProblemDetails title is "Invalid settings".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T7")]
    public async Task ResolveOutcomes_InvalidSettings_ProblemDetailIsTheResolverMessage()
    {
        // Then the ProblemDetails detail equals the resolver's exception message.
        await Task.CompletedTask;
    }

    #endregion
}
