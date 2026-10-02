// S3 — See and connect one exit per outcome: catalogue (docs/plans/action-outcomes/STORIES.md)
// Also covers S7 AC3 (catalogue labels returned raw).
//
// Pending: StepTypeItemResponseModel has no outcome fields yet. Follow CatalogueMapDefinitionTests
// for setup. The builder fills in each body and removes Skip.

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class CatalogueOutcomesMappingTests
{
    #region Given the yes/no action

    [Fact(Skip = "Pending: T6")]
    public void Map_YesNoAction_OutcomeKeysAreYesThenNo()
    {
        // Then the item's outcomes keys are [yes, no].
    }

    [Fact(Skip = "Pending: T6")]
    public void Map_YesNoAction_NoIsTheDefault()
    {
        // Then only "no" has isDefault = true.
    }

    [Fact(Skip = "Pending: T6")]
    public void Map_YesNoAction_HasDynamicOutcomesIsFalse()
    {
        // Then hasDynamicOutcomes is false.
    }

    [Fact(Skip = "Pending: T6")]
    public void Map_KeyLabel_IsMappedUntranslated()
    {
        // Given label "#uaOutcomes_found" — Then the item's label is "#uaOutcomes_found".
    }

    #endregion

    #region Given the dynamic options action

    [Fact(Skip = "Pending: T6")]
    public void Map_DynamicAction_HasDynamicOutcomesIsTrue()
    {
        // Then hasDynamicOutcomes is true.
    }

    #endregion

    #region Given an action that declares nothing

    [Fact(Skip = "Pending: T6")]
    public void Map_ActionDeclaringNothing_OutcomesIsEmptyNotNull()
    {
        // Then outcomes is an empty array.
    }

    [Fact(Skip = "Pending: T6")]
    public void Map_ActionDeclaringNothing_HasDynamicOutcomesIsFalse()
    {
        // Then hasDynamicOutcomes is false.
    }

    #endregion

    #region Given a trigger

    [Fact(Skip = "Pending: T6")]
    public void Map_Trigger_OutcomesIsEmpty()
    {
        // Then outcomes is an empty array.
    }

    #endregion
}
