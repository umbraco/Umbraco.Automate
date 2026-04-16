using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomateBackOfficeSecurityAccessorTests
{
    [Fact]
    public void BackOfficeSecurity_NoAsyncLocal_InnerReturnsNull_ReturnsNull()
    {
        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        accessor.BackOfficeSecurity.ShouldBeNull();
    }

    [Fact]
    public void BackOfficeSecurity_NoAsyncLocal_DelegatesToInner()
    {
        var innerSecurity = Mock.Of<IBackOfficeSecurity>();
        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns(innerSecurity);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        accessor.BackOfficeSecurity.ShouldBeSameAs(innerSecurity);
    }

    [Fact]
    public void Set_SetsAsyncLocalValue()
    {
        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var security = new AutomateBackOfficeSecurity(user);

        using var scope = AutomateBackOfficeSecurityAccessor.Set(security);

        accessor.BackOfficeSecurity.ShouldBeSameAs(security);
        accessor.BackOfficeSecurity!.CurrentUser.ShouldBeSameAs(user);
    }

    [Fact]
    public void Set_DisposeClearsValue()
    {
        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var security = new AutomateBackOfficeSecurity(user);

        var scope = AutomateBackOfficeSecurityAccessor.Set(security);
        accessor.BackOfficeSecurity.ShouldNotBeNull();

        scope.Dispose();
        accessor.BackOfficeSecurity.ShouldBeNull();
    }

    [Fact]
    public void AsyncLocal_TakesPrecedenceOverInner()
    {
        // Arrange: inner accessor returns an existing security context
        var innerUser = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var innerSecurity = new Mock<IBackOfficeSecurity>();
        innerSecurity.Setup(s => s.CurrentUser).Returns(innerUser);

        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns(innerSecurity.Object);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        // Without AsyncLocal, should return inner's security
        accessor.BackOfficeSecurity.ShouldBeSameAs(innerSecurity.Object);

        // With AsyncLocal set, should return the automation security
        var automationUser = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var automationSecurity = new AutomateBackOfficeSecurity(automationUser);

        using var scope = AutomateBackOfficeSecurityAccessor.Set(automationSecurity);
        accessor.BackOfficeSecurity.ShouldBeSameAs(automationSecurity);
        accessor.BackOfficeSecurity!.CurrentUser.ShouldBeSameAs(automationUser);
    }

    [Fact]
    public void AfterDispose_FallsBackToInner()
    {
        var innerSecurity = Mock.Of<IBackOfficeSecurity>();
        var inner = new Mock<IBackOfficeSecurityAccessor>();
        inner.Setup(a => a.BackOfficeSecurity).Returns(innerSecurity);

        var accessor = new AutomateBackOfficeSecurityAccessor(inner.Object);

        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var scope = AutomateBackOfficeSecurityAccessor.Set(new AutomateBackOfficeSecurity(user));
        scope.Dispose();

        // After dispose, should fall back to the inner accessor
        accessor.BackOfficeSecurity.ShouldBeSameAs(innerSecurity);
    }
}
