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

        // All-of: a step type that lists multiple sections genuinely touches all of them
        // and the user must have every one. Defers to CMS's case-insensitive HasSectionAccess
        // so we match Umbraco's comparison semantics for section aliases.
        foreach (var section in required)
        {
            if (!user.HasSectionAccess(section))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public bool HasRequiredPermissions(IUser user, IStepType stepType)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(stepType);

        var required = stepType.RequiredPermissions;
        if (required.Count == 0)
        {
            return true;
        }

        // Union the user's group default permissions. Granular per-node grants are not
        // considered — those belong to the runtime per-node authorisation done by
        // IAutomationActionAuthorizer. The catalogue and publish gates ask "can this account
        // do this anywhere?", which is the union of group letters.
        var grantedByGroups = user.Groups
            .SelectMany(g => g.Permissions)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var permission in required)
        {
            if (!grantedByGroups.Contains(permission))
            {
                return false;
            }
        }

        return true;
    }
}
