using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Tests.Unit.Security;

public class SectionAccessCheckerTests
{
    [Fact]
    public void Returns_true_when_step_type_requires_no_sections()
    {
        var stepType = StepTypeWithRequiredSections();
        var user = UserWithAllowedSections("content");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void Returns_true_when_user_has_the_single_required_section()
    {
        var stepType = StepTypeWithRequiredSections("users");
        var user = UserWithAllowedSections("content", "users");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void Returns_false_when_user_lacks_the_single_required_section()
    {
        var stepType = StepTypeWithRequiredSections("users");
        var user = UserWithAllowedSections("content");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeFalse();
    }

    [Fact]
    public void Returns_true_when_user_has_at_least_one_of_multiple_required_sections()
    {
        var stepType = StepTypeWithRequiredSections("members", "users");
        var user = UserWithAllowedSections("media", "users");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void Returns_false_when_user_has_none_of_multiple_required_sections()
    {
        var stepType = StepTypeWithRequiredSections("members", "users");
        var user = UserWithAllowedSections("content");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeFalse();
    }

    [Fact]
    public void Returns_false_when_user_has_no_allowed_sections()
    {
        var stepType = StepTypeWithRequiredSections("content");
        var user = UserWithAllowedSections();

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeFalse();
    }

    [Fact]
    public void Throws_when_user_is_null()
    {
        var stepType = StepTypeWithRequiredSections("content");
        Should.Throw<ArgumentNullException>(() => new SectionAccessChecker().CanAccess(null!, stepType));
    }

    [Fact]
    public void Throws_when_step_type_is_null()
    {
        var user = UserWithAllowedSections();
        Should.Throw<ArgumentNullException>(() => new SectionAccessChecker().CanAccess(user, null!));
    }

    private static IStepType StepTypeWithRequiredSections(params string[] sections)
    {
        var mock = new Mock<IStepType>();
        mock.Setup(s => s.RequiredSections).Returns(sections);
        return mock.Object;
    }

    private static IUser UserWithAllowedSections(params string[] sections)
    {
        var mock = new Mock<IUser>();
        mock.Setup(u => u.AllowedSections).Returns(sections);
        return mock.Object;
    }
}
