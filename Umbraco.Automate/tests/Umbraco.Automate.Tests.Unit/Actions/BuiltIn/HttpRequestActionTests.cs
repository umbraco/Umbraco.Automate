using System.Net;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Shouldly;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Configuration;
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
    public async Task ExecuteAsync_ResponseBodyAtLimit_Succeeds()
    {
        var body = new string('a', 1024);
        var action = CreateAction(HttpStatusCode.OK, body, maxResponseBodyBytes: 1024);

        var result = await action.ExecuteAsync(
            CreateContext(new HttpRequestSettings { Url = "https://example.com/api" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<HttpRequestOutput>().ResponseBody.ShouldBe(body);
    }

    [Fact]
    public async Task ExecuteAsync_ResponseWithContentLengthOverLimit_FailsWithActionableError()
    {
        var action = CreateAction(HttpStatusCode.OK, new string('a', 2000), maxResponseBodyBytes: 1024);

        var result = await action.ExecuteAsync(
            CreateContext(new HttpRequestSettings { Url = "https://example.com/api" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);

        // Terminal category — retrying re-downloads the same oversized payload.
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);

        // The error must tell the user the size, the limit, the config key, and a way out.
        var message = result.Exception!.Message;
        message.ShouldContain("2000");
        message.ShouldContain("1024");
        message.ShouldContain("Umbraco:Automate:Execution:MaxHttpResponseBodyBytes");
        message.ShouldContain("paginate");
    }

    [Fact]
    public async Task ExecuteAsync_ResponseWithoutContentLengthOverLimit_FailsWhileReading()
    {
        // No Content-Length header — the cap must be enforced while streaming the body.
        var action = CreateAction(
            HttpStatusCode.OK,
            content: new UnknownLengthContent(2000),
            maxResponseBodyBytes: 1024);

        var result = await action.ExecuteAsync(
            CreateContext(new HttpRequestSettings { Url = "https://example.com/api" }),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);

        var message = result.Exception!.Message;
        message.ShouldContain("1024");
        message.ShouldContain("Umbraco:Automate:Execution:MaxHttpResponseBodyBytes");
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaderRows_SendsEveryHeader()
    {
        HttpRequestMessage? sent = null;
        var action = CreateAction(HttpStatusCode.OK, "{}", req => sent = req);

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "GET",
            Headers =
            [
                new HttpRequestKeyValue { Key = "Authorization", Value = "Bearer token" },
                new HttpRequestKeyValue { Key = "X-Correlation-Id", Value = "abc-123" },

                // The key/value editor leaves an empty row behind whenever one is added and
                // not filled in; it must not fail the step.
                new HttpRequestKeyValue { Key = "", Value = "" },
            ],
        });

        await action.ExecuteAsync(context, CancellationToken.None);

        sent.ShouldNotBeNull();
        sent.Headers.GetValues("Authorization").ShouldBe(["Bearer token"]);
        sent.Headers.GetValues("X-Correlation-Id").ShouldBe(["abc-123"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaderValueButNoName_ReturnsValidationFailure()
    {
        // The old JSON blob swallowed malformed header input and sent the request without it.
        var action = CreateAction();

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Headers = [new HttpRequestKeyValue { Key = "  ", Value = "Bearer token" }],
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnusableHeaderName_ReturnsValidationFailure()
    {
        var action = CreateAction();

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Headers = [new HttpRequestKeyValue { Key = "Bad Header", Value = "x" }],
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        result.Exception!.Message.ShouldContain("Bad Header");
    }

    [Fact]
    public async Task ExecuteAsync_WithContentTypeHeaderRow_AppliesItToTheBody()
    {
        // Content headers belong on the body, not on the request header collection, and an
        // explicitly configured one wins over the Content Type setting.
        HttpRequestMessage? sent = null;
        var action = CreateAction(HttpStatusCode.OK, "{}", req => sent = req);

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "POST",
            Body = "<xml />",
            ContentType = "application/json",
            Headers = [new HttpRequestKeyValue { Key = "Content-Type", Value = "application/xml" }],
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        sent!.Content!.Headers.ContentType!.ToString().ShouldBe("application/xml");
    }

    [Fact]
    public async Task ExecuteAsync_FormBodyMode_SendsUrlEncodedBodyAndSetsContentType()
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
            BodyMode = HttpRequestBodyMode.Form,

            // Deliberately left at its default: a form post must not require the author to
            // also set the Content-Type by hand.
            ContentType = "application/json",
            FormFields =
            [
                new HttpRequestKeyValue { Key = "name", Value = "a b" },
                new HttpRequestKeyValue { Key = "note", Value = "x&y" },
                new HttpRequestKeyValue { Key = "", Value = "dropped" },
            ],
        });

        await action.ExecuteAsync(context, CancellationToken.None);

        sentBody.ShouldBe("name=a+b&note=x%26y");
        sentContentType.ShouldBe("application/x-www-form-urlencoded");
    }

    [Fact]
    public async Task ExecuteAsync_FormBodyMode_IgnoresTheRawBody()
    {
        string? sentBody = null;
        var action = CreateAction(HttpStatusCode.OK, "{}", req =>
            sentBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult());

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "POST",
            BodyMode = HttpRequestBodyMode.Form,
            Body = "{\"ignored\":true}",
            FormFields = [new HttpRequestKeyValue { Key = "a", Value = "b" }],
        });

        await action.ExecuteAsync(context, CancellationToken.None);

        sentBody.ShouldBe("a=b");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultBodyMode_IsRaw()
    {
        // Automations saved before the body mode existed deserialize to the default, and must
        // keep posting their raw body exactly as before.
        new HttpRequestSettings().BodyMode.ShouldBe(HttpRequestBodyMode.Raw);

        string? sentBody = null;
        var action = CreateAction(HttpStatusCode.OK, "{}", req =>
            sentBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult());

        var context = CreateContext(new HttpRequestSettings
        {
            Url = "https://example.com/api",
            Method = "POST",
            Body = "{\"hello\":\"world\"}",
        });

        await action.ExecuteAsync(context, CancellationToken.None);

        sentBody.ShouldBe("{\"hello\":\"world\"}");
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
        Action<HttpRequestMessage>? onRequest = null,
        HttpContent? content = null,
        long? maxResponseBodyBytes = null)
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
                Content = content ?? new StringContent(body),
            });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(Constants.HttpClients.Default)).Returns(client);

        var options = new ExecutionOptions();
        if (maxResponseBodyBytes.HasValue)
        {
            options.MaxHttpResponseBodyBytes = maxResponseBodyBytes.Value;
        }

        var deps = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());
        return new HttpRequestAction(deps, factory.Object, Options.Create(options));
    }

    /// <summary>
    /// HTTP content that never reports a Content-Length, forcing the cap to be enforced
    /// while the body streams.
    /// </summary>
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(int size)
        {
            _bytes = new byte[size];
            Array.Fill(_bytes, (byte)'a');
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
