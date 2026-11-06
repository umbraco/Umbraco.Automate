// Stories AN-6, AN-7: docs/plans/analytics/STORIES.md
// Compiled out until T8 adds the controller. The T8 builder removes the #if/#endif lines.
#if AUTOMATE_ANALYTICS
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Metrics.Controllers;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Analytics;

/// <summary>
/// <c>AnalyticsMetricsController</c>: workspace scoping through the shared base helper (SPEC R1) and
/// 400 for each query the service refuses (SPEC R10). Set up as <c>RunSummaryMetricsControllerTests</c> is.
/// </summary>
public class AnalyticsMetricsControllerTests
{
    private static readonly Guid Content = Guid.NewGuid();
    private static readonly Guid Commerce = Guid.NewGuid();

    public abstract class ControllerScenario
    {
        protected Mock<IAutomateAnalyticsService> Service { get; } = new();

        protected Mock<IWorkspaceService> Workspaces { get; } = new();

        protected Mock<IAuthorizationHelper> Authorization { get; } = new();

        protected IReadOnlySet<Guid>? ScopePassedToService { get; private set; }

        protected AnalyticsMetricsController Controller { get; }

        protected ControllerScenario()
        {
            Service
                .Setup(s => s.GetSummaryAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .Callback((AnalyticsQuery q, CancellationToken _) => ScopePassedToService = q.WorkspaceIds)
                .ReturnsAsync(Attempt.SucceedWithStatus(AnalyticsQueryStatus.Success, AnalyticsSummary.Empty));

            Controller = new AnalyticsMetricsController(Service.Object, Workspaces.Object, Authorization.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }

        protected void SignInAs(bool isAdmin, params Guid[] accessibleWorkspaces)
        {
            var user = new Mock<IUser>();
            user.Setup(u => u.Groups).Returns([]);
            user.Setup(u => u.IsAdmin()).Returns(isAdmin);
            Authorization.Setup(a => a.GetUmbracoUser(It.IsAny<System.Security.Principal.IPrincipal>())).Returns(user.Object);
            Workspaces
                .Setup(w => w.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accessibleWorkspaces.ToHashSet());
        }

        protected Task<IActionResult> GetSummary(Guid? workspaceId = null)
            => Controller.GetSummary(
                from: new DateOnly(2026, 10, 1),
                to: new DateOnly(2026, 10, 31),
                timeZone: "Europe/Copenhagen",
                workspaceId: workspaceId,
                cancellationToken: CancellationToken.None);
    }

    public class GivenANonAdminWithAccessToContentOnly : ControllerScenario
    {
        public GivenANonAdminWithAccessToContentOnly() => SignInAs(isAdmin: false, Content);

        [Fact(Skip = "Pending")]
        public async Task ScopesToTheirWorkspaces()
        {
            await GetSummary();

            ScopePassedToService.ShouldBe(new HashSet<Guid> { Content });
        }

        [Fact(Skip = "Pending")]
        public async Task AskingForCommerceGivesAnEmptyScope()
        {
            await GetSummary(Commerce);

            ScopePassedToService.ShouldBeEmpty();
        }

        [Fact(Skip = "Pending")]
        public async Task AskingForCommerceStillAnswers200()
            => (await GetSummary(Commerce)).ShouldBeOfType<OkObjectResult>();
    }

    public class GivenAnAdmin : ControllerScenario
    {
        public GivenAnAdmin() => SignInAs(isAdmin: true);

        [Fact(Skip = "Pending")]
        public async Task LeavesTheScopeOpen()
        {
            await GetSummary();

            ScopePassedToService.ShouldBeNull();
        }

        [Fact(Skip = "Pending")]
        public async Task NarrowsToOneWorkspaceWhenAsked()
        {
            await GetSummary(Commerce);

            ScopePassedToService.ShouldBe(new HashSet<Guid> { Commerce });
        }
    }

    public class GivenTheServiceRefusesTheQuery : ControllerScenario
    {
        public GivenTheServiceRefusesTheQuery() => SignInAs(isAdmin: true);

        [Theory(Skip = "Pending")]
        [InlineData(AnalyticsQueryStatus.UnknownTimeZone)]
        [InlineData(AnalyticsQueryStatus.RangeBackwards)]
        [InlineData(AnalyticsQueryStatus.RangeTooLong)]
        [InlineData(AnalyticsQueryStatus.IncompleteCompare)]
        [InlineData(AnalyticsQueryStatus.CompareLengthMismatch)]
        public async Task Answers400(AnalyticsQueryStatus status)
        {
            Service
                .Setup(s => s.GetSummaryAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Attempt.FailWithStatus<AnalyticsSummary?, AnalyticsQueryStatus>(status, null));

            var result = (ObjectResult)await GetSummary();

            result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        }
    }
}
#endif
