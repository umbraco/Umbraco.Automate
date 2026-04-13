using System.ComponentModel.DataAnnotations;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class DefaultStepErrorClassifierTests
{
    private readonly DefaultStepErrorClassifier _classifier = new();

    [Theory]
    [InlineData(StepRunErrorCategory.Validation)]
    [InlineData(StepRunErrorCategory.ConfigurationError)]
    [InlineData(StepRunErrorCategory.Authentication)]
    [InlineData(StepRunErrorCategory.Cancelled)]
    public void IsTerminal_TerminalCategories_ReturnsTrue(StepRunErrorCategory category)
        => _classifier.IsTerminal(category).ShouldBeTrue();

    [Theory]
    [InlineData(StepRunErrorCategory.Unknown)]
    [InlineData(StepRunErrorCategory.RateLimiting)]
    [InlineData(StepRunErrorCategory.Timeout)]
    [InlineData(StepRunErrorCategory.ServiceUnavailable)]
    [InlineData(StepRunErrorCategory.InvalidResponse)]
    public void IsTerminal_TransientCategories_ReturnsFalse(StepRunErrorCategory category)
        => _classifier.IsTerminal(category).ShouldBeFalse();

    [Fact]
    public void Classify_ValidationException_ReturnsValidation()
        => _classifier.Classify(new ValidationException("bad"))
            .ShouldBe(StepRunErrorCategory.Validation);

    [Fact]
    public void Classify_ArgumentException_ReturnsConfigurationError()
        => _classifier.Classify(new ArgumentException("bad arg"))
            .ShouldBe(StepRunErrorCategory.ConfigurationError);

    [Fact]
    public void Classify_FormatException_ReturnsConfigurationError()
        => _classifier.Classify(new FormatException("bad format"))
            .ShouldBe(StepRunErrorCategory.ConfigurationError);

    [Fact]
    public void Classify_InvalidOperationException_ReturnsConfigurationError()
        => _classifier.Classify(new InvalidOperationException("bad state"))
            .ShouldBe(StepRunErrorCategory.ConfigurationError);

    [Fact]
    public void Classify_TimeoutException_ReturnsTimeout()
        => _classifier.Classify(new TimeoutException())
            .ShouldBe(StepRunErrorCategory.Timeout);

    [Fact]
    public void Classify_OperationCanceledException_ReturnsCancelled()
        => _classifier.Classify(new OperationCanceledException())
            .ShouldBe(StepRunErrorCategory.Cancelled);

    [Fact]
    public void Classify_UnauthorizedAccessException_ReturnsAuthentication()
        => _classifier.Classify(new UnauthorizedAccessException())
            .ShouldBe(StepRunErrorCategory.Authentication);

    [Fact]
    public void Classify_UnknownException_ReturnsUnknown()
        => _classifier.Classify(new NotImplementedException())
            .ShouldBe(StepRunErrorCategory.Unknown);
}
