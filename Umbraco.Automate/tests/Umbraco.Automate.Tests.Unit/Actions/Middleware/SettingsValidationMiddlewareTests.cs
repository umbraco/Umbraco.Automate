using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;

namespace Umbraco.Automate.Tests.Unit.Actions.Middleware;

public class SettingsValidationMiddlewareTests
{
    private readonly SettingsValidationMiddleware _middleware;

    public SettingsValidationMiddlewareTests()
    {
        _middleware = new SettingsValidationMiddleware(Mock.Of<ILogger<SettingsValidationMiddleware>>());
    }

    private static ActionContext CreateContext(object? settings = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "test.action",
        Settings = settings,
    };

    [Fact]
    public async Task ApplyAsync_NullSettings_PassesThrough()
    {
        var expected = ActionResult.Success(new { Value = 1 });

        var result = await _middleware.ApplyAsync(
            CreateContext(settings: null),
            (_, _) => Task.FromResult(expected),
            CancellationToken.None);

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task ApplyAsync_ValidSettings_PassesThrough()
    {
        var settings = new ValidSettings { Name = "test", Url = "https://example.com" };
        var expected = ActionResult.Success(new { Value = 1 });

        var result = await _middleware.ApplyAsync(
            CreateContext(settings),
            (_, _) => Task.FromResult(expected),
            CancellationToken.None);

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task ApplyAsync_MissingRequiredField_ReturnsValidationFailure()
    {
        var settings = new ValidSettings { Name = null, Url = "https://example.com" };

        var result = await _middleware.ApplyAsync(
            CreateContext(settings),
            (_, _) => Task.FromResult(ActionResult.Success()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        result.Exception.ShouldBeOfType<ValidationException>();
    }

    [Fact]
    public async Task ApplyAsync_MultipleValidationErrors_ReportsAll()
    {
        var settings = new ValidSettings { Name = null, Url = null };

        var result = await _middleware.ApplyAsync(
            CreateContext(settings),
            (_, _) => Task.FromResult(ActionResult.Success()),
            CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        result.Exception!.Message.ShouldContain("Name");
        result.Exception.Message.ShouldContain("Url");
    }

    [Fact]
    public async Task ApplyAsync_InvalidSettings_DoesNotCallNext()
    {
        var settings = new ValidSettings { Name = null, Url = "https://example.com" };
        var nextCalled = false;

        await _middleware.ApplyAsync(
            CreateContext(settings),
            (_, _) =>
            {
                nextCalled = true;
                return Task.FromResult(ActionResult.Success());
            },
            CancellationToken.None);

        nextCalled.ShouldBeFalse();
    }

    private sealed class ValidSettings
    {
        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Url { get; set; }
    }
}
