// S8 — Invalid settings return a clear error from output schema too (docs/plans/action-outcomes/STORIES.md)
//
// Pending: the 400 handling isn't in ResolveStepTypeOutputSchemaController yet. Reuse the setup
// in ResolveStepTypeOutputSchemaControllerTests. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class ResolveStepTypeOutputSchemaInvalidSettingsTests
{
    #region Given valid settings

    [Fact(Skip = "Pending: T3")]
    public async Task ResolveOutputSchema_ValidSettings_StillReturnsOk()
    {
        // Then the result is the same 200 schema as today.
        await Task.CompletedTask;
    }

    #endregion

    #region Sad path: settings the settings type rejects

    [Fact(Skip = "Pending: T3")]
    public async Task ResolveOutputSchema_InvalidSettings_ReturnsBadRequest()
    {
        // Then the result is 400.
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T3")]
    public async Task ResolveOutputSchema_InvalidSettings_ProblemTitleIsInvalidSettings()
    {
        // Then the ProblemDetails title is "Invalid settings".
        await Task.CompletedTask;
    }

    [Fact(Skip = "Pending: T3")]
    public async Task ResolveOutputSchema_InvalidSettings_ProblemDetailIsTheResolverMessage()
    {
        // Then the ProblemDetails detail equals the resolver's exception message.
        await Task.CompletedTask;
    }

    #endregion
}
