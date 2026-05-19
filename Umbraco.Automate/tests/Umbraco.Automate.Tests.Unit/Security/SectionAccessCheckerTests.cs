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
    public void Returns_true_when_user_has_every_required_section()
    {
        var stepType = StepTypeWithRequiredSections("members", "users");
        var user = UserWithAllowedSections("media", "members", "users");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void Returns_false_when_user_has_only_some_of_multiple_required_sections()
    {
        // All-of semantics: a step type that lists multiple sections needs all of them.
        var stepType = StepTypeWithRequiredSections("members", "users");
        var user = UserWithAllowedSections("media", "users");

        new SectionAccessChecker().CanAccess(user, stepType).ShouldBeFalse();
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

    [Fact]
    public void HasRequiredPermissions_returns_true_when_step_type_requires_no_permissions()
    {
        var stepType = StepTypeWithRequiredPermissions();
        var user = UserWithGroupPermissions();

        new SectionAccessChecker().HasRequiredPermissions(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void HasRequiredPermissions_returns_true_when_user_group_covers_all_required_letters()
    {
        var stepType = StepTypeWithRequiredPermissions("Umb.Document.Publish");
        var user = UserWithGroupPermissions(("editors", new[] { "Umb.Document.Read", "Umb.Document.Update", "Umb.Document.Publish" }));

        new SectionAccessChecker().HasRequiredPermissions(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void HasRequiredPermissions_returns_false_when_no_group_has_the_required_letter()
    {
        var stepType = StepTypeWithRequiredPermissions("Umb.Document.Publish");
        var user = UserWithGroupPermissions(("editors", new[] { "Umb.Document.Read", "Umb.Document.Update" }));

        new SectionAccessChecker().HasRequiredPermissions(user, stepType).ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_unions_permissions_across_groups()
    {
        var stepType = StepTypeWithRequiredPermissions("Umb.Document.Read", "Umb.Document.Publish");
        var user = UserWithGroupPermissions(
            ("readers", new[] { "Umb.Document.Read" }),
            ("publishers", new[] { "Umb.Document.Publish" }));

        new SectionAccessChecker().HasRequiredPermissions(user, stepType).ShouldBeTrue();
    }

    [Fact]
    public void HasRequiredPermissions_returns_false_when_user_belongs_to_no_groups()
    {
        var stepType = StepTypeWithRequiredPermissions("Umb.Document.Publish");
        var user = UserWithGroupPermissions();

        new SectionAccessChecker().HasRequiredPermissions(user, stepType).ShouldBeFalse();
    }

    private static IStepType StepTypeWithRequiredSections(params string[] sections)
    {
        var mock = new Mock<IStepType>();
        mock.Setup(s => s.RequiredSections).Returns(sections);
        return mock.Object;
    }

    private static IStepType StepTypeWithRequiredPermissions(params string[] permissions)
    {
        var mock = new Mock<IStepType>();
        mock.Setup(s => s.RequiredPermissions).Returns(permissions);
        return mock.Object;
    }

    private static IUser UserWithAllowedSections(params string[] sections)
    {
        var mock = new Mock<IUser>();
        mock.Setup(u => u.AllowedSections).Returns(sections);
        return mock.Object;
    }

    private static IUser UserWithGroupPermissions(params (string Alias, string[] Permissions)[] groups)
    {
        var groupMocks = groups.Select(g =>
        {
            var groupMock = new Mock<IReadOnlyUserGroup>();
            groupMock.Setup(x => x.Alias).Returns(g.Alias);
            groupMock.Setup(x => x.Permissions).Returns(new HashSet<string>(g.Permissions));
            return groupMock.Object;
        }).ToArray();

        var userMock = new Mock<IUser>();
        userMock.Setup(u => u.Groups).Returns(groupMocks);
        return userMock.Object;
    }
}
