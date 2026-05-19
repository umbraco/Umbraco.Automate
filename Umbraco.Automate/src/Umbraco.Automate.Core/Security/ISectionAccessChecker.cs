using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Checks whether a user (typically a workspace's service account) has the backoffice section
/// access required to use a given step type.
/// </summary>
public interface ISectionAccessChecker
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="user"/> may use <paramref name="stepType"/>.
    /// </summary>
    /// <remarks>
    /// Semantics: an empty <see cref="IStepType.RequiredSections"/> always returns <c>true</c>
    /// (lenient default — unannotated third-party step types are usable everywhere). A non-empty
    /// list enforces any-of: the user's <see cref="IUser.AllowedSections"/> must contain at least
    /// one of the listed section aliases.
    /// </remarks>
    bool CanAccess(IUser user, IStepType stepType);
}
