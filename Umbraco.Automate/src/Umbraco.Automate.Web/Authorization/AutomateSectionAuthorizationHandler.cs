using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Api.Management.Security.Authorization;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Web.Authorization;

/// <summary>
/// Authorizes access to the Automate section by checking the current user's allowed sections,
/// replacing the obsolete <c>AllowedApplicationsClaimType</c> claim check removed in CMS v18.
/// </summary>
internal sealed class AutomateSectionAuthorizationHandler : MustSatisfyRequirementAuthorizationHandler<AutomateSectionRequirement>
{
    private readonly IAuthorizationHelper _authorizationHelper;

    public AutomateSectionAuthorizationHandler(IAuthorizationHelper authorizationHelper)
        => _authorizationHelper = authorizationHelper;

    protected override Task<bool> IsAuthorized(AuthorizationHandlerContext context, AutomateSectionRequirement requirement)
    {
        var allowed = _authorizationHelper.TryGetUmbracoUser(context.User, out IUser? user)
                      && user.AllowedSections.Contains(Core.Constants.Sections.Automate);
        return Task.FromResult(allowed);
    }
}
