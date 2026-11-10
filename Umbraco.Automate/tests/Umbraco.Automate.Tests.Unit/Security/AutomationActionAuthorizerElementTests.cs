using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomationActionAuthorizerElementTests
{
    private static readonly IReadOnlySet<string> Update = new HashSet<string> { ActionElementUpdate.ActionLetter };
    private static readonly IReadOnlySet<string> Create = new HashSet<string> { ActionElementNew.ActionLetter };
    private static readonly IReadOnlySet<string> Publish = new HashSet<string> { ActionElementPublish.ActionLetter };

    private readonly Mock<IContentPermissionService> _content = new();
    private readonly Mock<IMediaPermissionService> _media = new();
    private readonly Mock<IElementPermissionService> _elements = new();
    private readonly Mock<IElementContainerPermissionService> _folders = new();
    private readonly Mock<IEntityService> _entityService = new();
    private readonly IUser _serviceAccount;
    private readonly AutomationActionAuthorizer _sut;

    public AutomationActionAuthorizerElementTests()
    {
        _serviceAccount = MockUser();

        // Fail closed by default. Success is the zero value of every CMS authorization status
        // enum, which is what an unconfigured mock returns, so without these an element check
        // routed to the wrong service would still read as success.
        _elements
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess);
        _elements
            .Setup(s => s.AuthorizeRootAccessAsync(It.IsAny<IUser>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ElementAuthorizationStatus.UnauthorizedMissingRootAccess);
        _folders
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess);
        _folders
            .Setup(s => s.AuthorizeRootAccessAsync(It.IsAny<IUser>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ElementAuthorizationStatus.UnauthorizedMissingRootAccess);
        _content
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess);
        _content
            .Setup(s => s.AuthorizeRootAccessAsync(It.IsAny<IUser>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingRootAccess);

        var security = new Mock<IBackOfficeSecurity>();
        security.Setup(s => s.CurrentUser).Returns(_serviceAccount);
        var accessor = new Mock<IBackOfficeSecurityAccessor>();
        accessor.Setup(a => a.BackOfficeSecurity).Returns(security.Object);

        _sut = new AutomationActionAuthorizer(
            _content.Object,
            _media.Object,
            _elements.Object,
            _folders.Object,
            accessor.Object,
            _entityService.Object,
            NullLogger<AutomationActionAuthorizer>.Instance);
    }

    #region Happy path

    // STORY-1 AC1: Element in reach
    [Fact]
    public async Task AuthorizeElementAsync_returns_success_when_cms_authorises_the_element()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.Success);

        var result = await _sut.AuthorizeElementAsync(elementKey, Update, default);

        result.ShouldBe(AutomationAuthorizationResult.Success);
    }

    // STORY-1 AC1: Element in reach (explicit-user overload, used at dispatch time)
    [Fact]
    public async Task AuthorizeElementAsync_for_an_explicit_user_returns_success_when_cms_authorises_the_element()
    {
        var user = MockUser();
        var elementKey = Guid.NewGuid();
        SetupElement(user, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.Success);

        var result = await _sut.AuthorizeElementAsync(user, elementKey, Update, default);

        result.ShouldBe(AutomationAuthorizationResult.Success);
    }

    // STORY-1 AC2: Folder in reach
    [Fact]
    public async Task AuthorizeElementParentAsync_returns_success_when_cms_authorises_the_folder()
    {
        var folderKey = Guid.NewGuid();
        SetupFolder(folderKey, ActionElementNew.ActionLetter, ElementAuthorizationStatus.Success);

        var result = await _sut.AuthorizeElementParentAsync(folderKey, Create, default);

        result.ShouldBe(AutomationAuthorizationResult.Success);
    }

    // STORY-1 AC3: Library root
    [Fact]
    public async Task AuthorizeElementParentAsync_returns_success_at_the_root_when_cms_authorises_root_access()
    {
        _elements
            .Setup(s => s.AuthorizeRootAccessAsync(
                _serviceAccount,
                It.Is<ISet<string>>(p => p.Contains(ActionElementNew.ActionLetter))))
            .ReturnsAsync(ElementAuthorizationStatus.Success);

        var result = await _sut.AuthorizeElementParentAsync(null, Create, default);

        result.ShouldBe(AutomationAuthorizationResult.Success);
    }

    #endregion

    #region Sad path

    // STORY-1 AC4: Permission missing
    [Fact]
    public async Task AuthorizeElementAsync_fails_naming_the_permission_when_cms_reports_it_missing()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementPublish.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess);

        var result = await _sut.AuthorizeElementAsync(elementKey, Publish, default);

        (result.Authorized, result.IsNotFound, result.FailureReason?.Contains(ActionElementPublish.ActionLetter))
            .ShouldBe((false, false, true));
    }

    // STORY-1 AC5: Element outside the start node
    [Fact]
    public async Task AuthorizeElementAsync_fails_when_a_live_element_is_outside_the_start_node()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPathAccess);
        SetupPath(UmbracoObjectTypes.Element, "-1,1100,1234");

        var result = await _sut.AuthorizeElementAsync(elementKey, Update, default);

        (result.Authorized, result.IsNotFound).ShouldBe((false, false));
    }

    // STORY-1 AC6: Folder permission takes it away
    // The CMS resolves a granular permission set on the element's folder through the element's
    // path, and reports it as a missing permission. The acceptance spec ("Folder permissions",
    // STORY-1 AC14) covers the real resolution; here the authorizer must pass that refusal on.
    [Fact]
    public async Task AuthorizeElementAsync_fails_when_a_folder_permission_removes_the_verb()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPermissionAccess);

        var result = await _sut.AuthorizeElementAsync(elementKey, Update, default);

        (result.Authorized, result.IsNotFound).ShouldBe((false, false));
    }

    // STORY-1 AC7: Folder outside the start node
    [Fact]
    public async Task AuthorizeElementParentAsync_fails_when_a_live_folder_is_outside_the_start_node()
    {
        var folderKey = Guid.NewGuid();
        SetupFolder(folderKey, ActionElementNew.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPathAccess);
        SetupPath(UmbracoObjectTypes.ElementContainer, "-1,1100,1234");

        var result = await _sut.AuthorizeElementParentAsync(folderKey, Create, default);

        (result.Authorized, result.IsNotFound).ShouldBe((false, false));
    }

    // STORY-1 AC8: No root access
    [Fact]
    public async Task AuthorizeElementParentAsync_fails_at_the_root_without_root_access()
    {
        _elements
            .Setup(s => s.AuthorizeRootAccessAsync(_serviceAccount, It.IsAny<ISet<string>>()))
            .ReturnsAsync(ElementAuthorizationStatus.UnauthorizedMissingRootAccess);

        var result = await _sut.AuthorizeElementParentAsync(null, Create, default);

        (result.Authorized, result.IsNotFound).ShouldBe((false, false));
    }

    // STORY-1 AC9: Element missing
    [Fact]
    public async Task AuthorizeElementAsync_flags_not_found_when_cms_reports_not_found()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.NotFound);

        var result = await _sut.AuthorizeElementAsync(elementKey, Update, default);

        result.IsNotFound.ShouldBeTrue();
    }

    // STORY-1 AC9: Element missing (explicit-user overload)
    [Fact]
    public async Task AuthorizeElementAsync_for_an_explicit_user_flags_not_found_when_cms_reports_not_found()
    {
        var user = MockUser();
        var elementKey = Guid.NewGuid();
        SetupElement(user, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.NotFound);

        var result = await _sut.AuthorizeElementAsync(user, elementKey, Update, default);

        result.IsNotFound.ShouldBeTrue();
    }

    // STORY-1 AC10: Trashed element, scoped account
    // The CMS reports a trashed element as missing path access to an account with a folder start
    // node, because the Library recycle bin (-1,-22,) sits outside every start-node path.
    [Fact]
    public async Task AuthorizeElementAsync_flags_not_found_for_a_trashed_element_denied_on_path()
    {
        var elementKey = Guid.NewGuid();
        SetupElement(_serviceAccount, elementKey, ActionElementUpdate.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPathAccess);
        SetupPath(UmbracoObjectTypes.Element, "-1,-22,1234");

        var result = await _sut.AuthorizeElementAsync(elementKey, Update, default);

        result.IsNotFound.ShouldBeTrue();
    }

    // STORY-1 AC11: Trashed folder, scoped account
    [Fact]
    public async Task AuthorizeElementParentAsync_flags_not_found_for_a_trashed_folder_denied_on_path()
    {
        var folderKey = Guid.NewGuid();
        SetupFolder(folderKey, ActionElementNew.ActionLetter, ElementAuthorizationStatus.UnauthorizedMissingPathAccess);
        SetupPath(UmbracoObjectTypes.ElementContainer, "-1,-22,1234");

        var result = await _sut.AuthorizeElementParentAsync(folderKey, Create, default);

        result.IsNotFound.ShouldBeTrue();
    }

    #endregion

    private static IUser MockUser()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(Guid.NewGuid());
        return user.Object;
    }

    private void SetupElement(IUser user, Guid elementKey, string permission, ElementAuthorizationStatus status)
        => _elements
            .Setup(s => s.AuthorizeAccessAsync(
                user,
                It.Is<IEnumerable<Guid>>(keys => keys.Contains(elementKey)),
                It.Is<ISet<string>>(p => p.Contains(permission))))
            .ReturnsAsync(status);

    private void SetupFolder(Guid folderKey, string permission, ElementAuthorizationStatus status)
        => _folders
            .Setup(s => s.AuthorizeAccessAsync(
                _serviceAccount,
                It.Is<IEnumerable<Guid>>(keys => keys.Contains(folderKey)),
                It.Is<ISet<string>>(p => p.Contains(permission))))
            .ReturnsAsync(status);

    private void SetupPath(UmbracoObjectTypes type, string path)
        => _entityService
            .Setup(e => e.GetAllPaths(type, It.IsAny<Guid[]>()))
            .Returns([new TreeEntityPath { Id = 1234, Path = path }]);
}
