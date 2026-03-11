using Umbraco.Cms.Core.Packaging;

namespace Umbraco.Automate.Core.Migrations;

/// <summary>
/// Migration plan for the Umbraco.Automate package.
/// </summary>
public class UmbracoAutomateMigrationPlan : PackageMigrationPlan
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UmbracoAutomateMigrationPlan"/> class.
    /// </summary>
    public UmbracoAutomateMigrationPlan()
        : base("Umbraco.Automate", "Umbraco.Automate", "UmbracoAutomate")
    { }

    /// <inheritdoc />
    public override string InitialState => "{ua-init-state}";

    /// <inheritdoc />
    public override bool IgnoreCurrentState => false;

    /// <inheritdoc />
    protected override void DefinePlan()
    {
        From("{ua-init-state}")
            .To<AddAutomateSectionToAdminGroup>("5A8F2D3E-1B4C-4E6A-9F0D-7C2E8B5A3D1F");
    }
}
