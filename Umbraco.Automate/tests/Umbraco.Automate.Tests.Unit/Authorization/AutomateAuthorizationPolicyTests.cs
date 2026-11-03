using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using Shouldly;
using Umbraco.Automate.Web.Authorization;
using CmsConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Tests.Unit.Authorization;

public class AutomateAuthorizationPolicyTests
{
    private static AuthorizationPolicy GetPolicy(string policyName)
    {
        var options = new AuthorizationOptions();
        AutomateAuthorizationPolicyConfiguration.Configure(options);
        return options.GetPolicy(policyName).ShouldNotBeNull();
    }

    [Theory]
    [InlineData(AutomateAuthorizationPolicies.SectionAccessAutomate)]
    [InlineData(AutomateAuthorizationPolicies.WorkspaceAccess)]
    public void Policy_AcceptsBackOfficeCookieAndBearerToken(string policyName)
    {
        var policy = GetPolicy(policyName);

        policy.AuthenticationSchemes.Count.ShouldBe(2);
        policy.AuthenticationSchemes.ShouldContain(CmsConstants.Security.BackOfficeAuthenticationType);
        policy.AuthenticationSchemes.ShouldContain(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
    }

    [Fact]
    public void SectionAccessPolicy_KeepsItsRequirement()
        => GetPolicy(AutomateAuthorizationPolicies.SectionAccessAutomate)
            .Requirements.ShouldContain(r => r is AutomateSectionRequirement);

    [Fact]
    public void WorkspaceAccessPolicy_KeepsItsRequirement()
        => GetPolicy(AutomateAuthorizationPolicies.WorkspaceAccess)
            .Requirements.ShouldContain(r => r is WorkspaceAccessRequirement);
}
