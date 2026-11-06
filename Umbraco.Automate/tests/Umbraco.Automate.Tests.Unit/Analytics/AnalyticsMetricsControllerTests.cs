// Stories AN-6, AN-7: docs/plans/analytics/STORIES.md
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Metrics.Controllers;
using Umbraco.Automate.Web.Api.Management.Metrics.Mapping;
using Umbraco.Automate.Web.Api.Management.Metrics.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Tests.Unit.Analytics;

/// <summary>
/// <c>AnalyticsMetricsController</c>: workspace scoping through the shared base helper (SPEC R1) and
/// 400 for each query the service refuses (SPEC R10), on each of its three endpoints.
/// Set up as <c>RunSummaryMetricsControllerTests</c> is.
/// </summary>
public class AnalyticsMetricsControllerTests
{
    private static readonly Guid Content = Guid.NewGuid();
    private static readonly Guid Commerce = Guid.NewGuid();

    public enum Endpoint
    {
        Summary,
        Automations,
        Actions,
    }

    public static TheoryData<Endpoint> Endpoints => new(Enum.GetValues<Endpoint>());

    public static TheoryData<Endpoint, AnalyticsQueryStatus> EndpointsAndRefusals
    {
        get
        {
            var data = new TheoryData<Endpoint, AnalyticsQueryStatus>();
            foreach (var endpoint in Enum.GetValues<Endpoint>())
            {
                foreach (var status in Enum.GetValues<AnalyticsQueryStatus>().Where(s => s != AnalyticsQueryStatus.Success))
                {
                    data.Add(endpoint, status);
                }
            }

            return data;
        }
    }

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
                .ReturnsAsync(Attempt.SucceedWithStatus<AnalyticsSummary?, AnalyticsQueryStatus>(
                    AnalyticsQueryStatus.Success,
                    new AnalyticsSummary { Current = new AnalyticsPeriod { Days = [] } }));
            Service
                .Setup(s => s.GetAutomationsAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .Callback((AnalyticsQuery q, CancellationToken _) => ScopePassedToService = q.WorkspaceIds)
                .ReturnsAsync(Attempt.SucceedWithStatus<AnalyticsRows<AutomationAnalyticsRow>?, AnalyticsQueryStatus>(
                    AnalyticsQueryStatus.Success,
                    new AnalyticsRows<AutomationAnalyticsRow> { Items = [] }));
            Service
                .Setup(s => s.GetActionsAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                .Callback((AnalyticsQuery q, CancellationToken _) => ScopePassedToService = q.WorkspaceIds)
                .ReturnsAsync(Attempt.SucceedWithStatus<AnalyticsRows<ActionAnalyticsRow>?, AnalyticsQueryStatus>(
                    AnalyticsQueryStatus.Success,
                    new AnalyticsRows<ActionAnalyticsRow> { Items = [] }));

            var mapDefinition = new AnalyticsMapDefinition();
            var mapper = new UmbracoMapper(
                new MapDefinitionCollection(() => [mapDefinition]),
                new Mock<ICoreScopeProvider> { DefaultValue = DefaultValue.Mock }.Object,
                Mock.Of<ILogger<UmbracoMapper>>());

            Controller = new AnalyticsMetricsController(Service.Object, Workspaces.Object, Authorization.Object, mapper)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }

        protected void SignInAs(bool isAdmin, params Guid[] accessibleWorkspaces)
        {
            var user = new Mock<IUser>();
            var groups = new List<IReadOnlyUserGroup>();
            if (isAdmin)
            {
                var adminGroup = new Mock<IReadOnlyUserGroup>();
                adminGroup.Setup(g => g.Alias).Returns(Umbraco.Cms.Core.Constants.Security.AdminGroupAlias);
                adminGroup.Setup(g => g.Key).Returns(Guid.NewGuid());
                groups.Add(adminGroup.Object);
            }

            user.Setup(u => u.Groups).Returns(groups);
            Authorization.Setup(a => a.GetUmbracoUser(It.IsAny<System.Security.Principal.IPrincipal>())).Returns(user.Object);
            Workspaces
                .Setup(w => w.GetAccessibleWorkspaceIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(accessibleWorkspaces.ToHashSet());
        }

        protected void ServiceRefuses(Endpoint endpoint, AnalyticsQueryStatus status)
        {
            switch (endpoint)
            {
                case Endpoint.Summary:
                    Service
                        .Setup(s => s.GetSummaryAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Attempt.FailWithStatus<AnalyticsSummary?, AnalyticsQueryStatus>(status, null));
                    break;
                case Endpoint.Automations:
                    Service
                        .Setup(s => s.GetAutomationsAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Attempt.FailWithStatus<AnalyticsRows<AutomationAnalyticsRow>?, AnalyticsQueryStatus>(status, null));
                    break;
                default:
                    Service
                        .Setup(s => s.GetActionsAsync(It.IsAny<AnalyticsQuery>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Attempt.FailWithStatus<AnalyticsRows<ActionAnalyticsRow>?, AnalyticsQueryStatus>(status, null));
                    break;
            }
        }

        protected Task<IActionResult> Call(Endpoint endpoint, Guid? workspaceId = null, DateOnly? to = null)
        {
            var query = new AnalyticsQueryRequestModel
            {
                From = new DateOnly(2026, 10, 1),
                To = to ?? new DateOnly(2026, 10, 31),
                TimeZone = "Europe/Copenhagen",
                WorkspaceId = workspaceId,
            };

            return endpoint switch
            {
                Endpoint.Summary => Controller.GetSummary(query, CancellationToken.None),
                Endpoint.Automations => Controller.GetAutomations(query, CancellationToken.None),
                _ => Controller.GetActions(query, CancellationToken.None),
            };
        }

        protected async Task<ProblemDetails> ProblemFrom(Endpoint endpoint, DateOnly? to = null)
            => (ProblemDetails)((ObjectResult)await Call(endpoint, to: to)).Value!;
    }

    public class GivenANonAdminWithAccessToContentOnly : ControllerScenario
    {
        public GivenANonAdminWithAccessToContentOnly() => SignInAs(isAdmin: false, Content);

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task ScopesToTheirWorkspaces(Endpoint endpoint)
        {
            await Call(endpoint);

            ScopePassedToService.ShouldBe(new HashSet<Guid> { Content });
        }

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task AskingForCommerceGivesAnEmptyScope(Endpoint endpoint)
        {
            await Call(endpoint, Commerce);

            ScopePassedToService.ShouldBeEmpty();
        }

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task AskingForCommerceStillAnswers200(Endpoint endpoint)
            => (await Call(endpoint, Commerce)).ShouldBeOfType<OkObjectResult>();
    }

    public class GivenAnAdmin : ControllerScenario
    {
        public GivenAnAdmin() => SignInAs(isAdmin: true);

        [Fact]
        public async Task LeavesTheScopeOpen()
        {
            await Call(Endpoint.Summary);

            ScopePassedToService.ShouldBeNull();
        }

        [Fact]
        public async Task NarrowsToOneWorkspaceWhenAsked()
        {
            await Call(Endpoint.Summary, Commerce);

            ScopePassedToService.ShouldBe(new HashSet<Guid> { Commerce });
        }
    }

    public class GivenTheServiceRefusesTheQuery : ControllerScenario
    {
        public GivenTheServiceRefusesTheQuery() => SignInAs(isAdmin: true);

        [Theory]
        [MemberData(nameof(EndpointsAndRefusals), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task Answers400(Endpoint endpoint, AnalyticsQueryStatus status)
        {
            ServiceRefuses(endpoint, status);

            var result = (ObjectResult)await Call(endpoint);

            result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        }

        [Theory]
        [MemberData(nameof(EndpointsAndRefusals), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task TheBodyHasAType(Endpoint endpoint, AnalyticsQueryStatus status)
        {
            ServiceRefuses(endpoint, status);

            (await ProblemFrom(endpoint)).Type.ShouldNotBeNullOrEmpty();
        }

        [Theory]
        [MemberData(nameof(EndpointsAndRefusals), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task TheBodyCarriesTheOperationStatus(Endpoint endpoint, AnalyticsQueryStatus status)
        {
            ServiceRefuses(endpoint, status);

            (await ProblemFrom(endpoint)).Extensions["operationStatus"].ShouldBe(status.ToString());
        }

        [Fact]
        public async Task TheBodyHasNoStatusExtensionOfItsOwn()
        {
            ServiceRefuses(Endpoint.Summary, AnalyticsQueryStatus.RangeBackwards);

            (await ProblemFrom(Endpoint.Summary)).Extensions.ShouldNotContainKey("status");
        }
    }

    public class GivenADateAtTheEdgeOfTheCalendar : ControllerScenario
    {
        public GivenADateAtTheEdgeOfTheCalendar() => SignInAs(isAdmin: true);

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task Answers400(Endpoint endpoint)
        {
            var result = (ObjectResult)await Call(endpoint, to: DateOnly.MaxValue);

            result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        }

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task DoesNotCallTheService(Endpoint endpoint)
        {
            await Call(endpoint, to: DateOnly.MaxValue);

            Service.Invocations.Count.ShouldBe(0);
        }

        [Theory]
        [MemberData(nameof(Endpoints), MemberType = typeof(AnalyticsMetricsControllerTests))]
        public async Task TheBodyHasAType(Endpoint endpoint)
            => (await ProblemFrom(endpoint, DateOnly.MaxValue)).Type.ShouldNotBeNullOrEmpty();
    }
}
