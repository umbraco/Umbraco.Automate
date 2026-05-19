using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Security;

/// <inheritdoc />
internal sealed class SectionAccessChecker : ISectionAccessChecker
{
    /// <inheritdoc />
    public bool CanAccess(IUser user, IStepType stepType)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(stepType);

        var required = stepType.RequiredSections;
        if (required.Count == 0)
        {
            return true;
        }

        // Defer to the CMS extension so we match Umbraco's case-insensitive comparison
        // semantics for section aliases (configured sections may be cased differently to
        // the constants).
        foreach (var section in required)
        {
            if (user.HasSectionAccess(section))
            {
                return true;
            }
        }

        return false;
    }
}
