using Microsoft.AspNetCore.Http;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomateBackOfficeSecurityAccessorTests
{
    [Fact]
    public void BackOfficeSecurity_NoAsyncLocal_NoHttpContext_ReturnsNull()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(httpContextAccessor.Object);

        accessor.BackOfficeSecurity.ShouldBeNull();
    }

    [Fact]
    public void Set_SetsAsyncLocalValue()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(httpContextAccessor.Object);

        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var security = new AutomateBackOfficeSecurity(user);

        using var scope = AutomateBackOfficeSecurityAccessor.Set(security);

        accessor.BackOfficeSecurity.ShouldBeSameAs(security);
        accessor.BackOfficeSecurity!.CurrentUser.ShouldBeSameAs(user);
    }

    [Fact]
    public void Set_DisposeClearsValue()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns((HttpContext?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(httpContextAccessor.Object);

        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var security = new AutomateBackOfficeSecurity(user);

        var scope = AutomateBackOfficeSecurityAccessor.Set(security);
        accessor.BackOfficeSecurity.ShouldNotBeNull();

        scope.Dispose();
        accessor.BackOfficeSecurity.ShouldBeNull();
    }

    [Fact]
    public void AsyncLocal_TakesPrecedenceOverHttpContext()
    {
        // Arrange: HTTP context has its own IBackOfficeSecurity
        var httpUser = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var httpSecurity = new Mock<IBackOfficeSecurity>();
        httpSecurity.Setup(s => s.CurrentUser).Returns(httpUser);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IBackOfficeSecurity)))
            .Returns(httpSecurity.Object);

        var httpContext = new Mock<HttpContext>();
        httpContext.Setup(c => c.RequestServices).Returns(serviceProvider.Object);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(h => h.HttpContext).Returns(httpContext.Object);

        var accessor = new AutomateBackOfficeSecurityAccessor(httpContextAccessor.Object);

        // Without AsyncLocal, should return HTTP context's security
        accessor.BackOfficeSecurity.ShouldBeSameAs(httpSecurity.Object);

        // With AsyncLocal set, should return the automation security
        var automationUser = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var automationSecurity = new AutomateBackOfficeSecurity(automationUser);

        using var scope = AutomateBackOfficeSecurityAccessor.Set(automationSecurity);
        accessor.BackOfficeSecurity.ShouldBeSameAs(automationSecurity);
        accessor.BackOfficeSecurity!.CurrentUser.ShouldBeSameAs(automationUser);
    }
}
