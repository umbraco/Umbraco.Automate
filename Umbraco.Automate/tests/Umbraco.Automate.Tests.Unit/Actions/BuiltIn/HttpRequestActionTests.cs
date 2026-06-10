using System.Net;
using Moq;
using Moq.Protected;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class HttpRequestActionTests
{
    [Fact]
    public void HasCorrectAlias()
    {
        var action = CreateAction();
        action.Alias.ShouldBe("umbracoAutomate.httpRequest");
    }

    [Fact]
    public void HasSettingsType()
    {
        var action = CreateAction();
        action.SettingsType.ShouldBe(typeof(HttpRequestSettings));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulGet_ReturnsSuccess()
    {
        var action = CreateAction(HttpStatusCode.OK, "{\"ok\":true}");

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "GET",
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<HttpRequestOutput>();
        output.StatusCode.ShouldBe(200);
        output.IsSuccess.ShouldBeTrue();
        output.ResponseBody.ShouldBe("{\"ok\":true}");
    }

    [Fact]
    public async Task ExecuteAsync_ServerError_ReturnsFailedWithCategory()
    {
        var action = CreateAction(HttpStatusCode.InternalServerError, "error");

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "POST",
            Body = "{\"data\":1}",
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.InvalidResponse);
    }

    [Fact]
    public async Task ExecuteAsync_Post_SendsBodyFromSettings()
    {
        string? sentBody = null;
        string? sentContentType = null;
        var action = CreateAction(HttpStatusCode.OK, "{}", req =>
        {
            sentBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            sentContentType = req.Content?.Headers.ContentType?.ToString();
        });

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "POST",
            Body = "{\"hello\":\"world\"}",
            ContentType = "application/json",
        });

        await action.ExecuteAsync(context, CancellationToken.None);

        sentBody.ShouldBe("{\"hello\":\"world\"}");

        // Content-Type must be sent verbatim, without a "; charset=utf-8" suffix that
        // strict webhook receivers reject.
        sentContentType.ShouldBe("application/json");
    }

    [Fact]
    public async Task ExecuteAsync_MissingUrl_ReturnsValidationFailure()
    {
        var action = CreateAction();

        var context = CreateContext(new HttpRequestSettings { Url = "" });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    private static ActionContext CreateContext(HttpRequestSettings settings) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.httpRequest",
        Settings = settings,
    };

    private static HttpRequestAction CreateAction(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string body = "",
        Action<HttpRequestMessage>? onRequest = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => onRequest?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body),
            });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("UmbracoAutomate")).Returns(client);

        var deps = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());
        return new HttpRequestAction(deps, factory.Object);
    }
}
