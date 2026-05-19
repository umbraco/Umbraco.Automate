using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Models.Membership;

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
        if (required is null || required.Count == 0)
        {
            return true;
        }

        var allowed = user.AllowedSections;
        if (allowed is null)
        {
            return false;
        }

        var allowedSet = allowed as ISet<string> ?? new HashSet<string>(allowed, StringComparer.Ordinal);

        foreach (var section in required)
        {
            if (allowedSet.Contains(section))
            {
                return true;
            }
        }

        return false;
    }
}
