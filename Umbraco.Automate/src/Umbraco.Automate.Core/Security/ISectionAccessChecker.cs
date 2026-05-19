using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Checks whether a user (typically a workspace's service account) has the backoffice access
/// — section membership and global permission letters — required to use a given step type.
/// </summary>
public interface ISectionAccessChecker
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="user"/> satisfies the step type's
    /// <see cref="IStepType.RequiredSections"/>.
    /// </summary>
    /// <remarks>
    /// Semantics: an empty <c>RequiredSections</c> always returns <c>true</c> (lenient default).
    /// A non-empty list enforces all-of: the user's <see cref="IUser.AllowedSections"/> must
    /// contain every listed section alias. A step type that declares multiple sections
    /// genuinely touches all of them — partial access is denial.
    /// </remarks>
    bool CanAccess(IUser user, IStepType stepType);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="user"/> satisfies the step type's
    /// <see cref="IStepType.RequiredPermissions"/> at the group level.
    /// </summary>
    /// <remarks>
    /// Semantics: an empty <c>RequiredPermissions</c> always returns <c>true</c>. A non-empty
    /// list enforces all-of: the union of the user's group permissions must contain every
    /// required letter. Granular per-node permissions that grant a verb on a specific node
    /// even when no group has it globally are not considered here — they belong to the
    /// runtime per-node authorisation done by <see cref="IAutomationActionAuthorizer"/>.
    /// Use this for catalogue and publish-time filtering, where "can this account do this
    /// anywhere?" is the right granularity.
    /// </remarks>
    bool HasRequiredPermissions(IUser user, IStepType stepType);
}
