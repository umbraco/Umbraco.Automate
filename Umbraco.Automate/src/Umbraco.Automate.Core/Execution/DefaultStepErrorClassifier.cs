using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Default <see cref="IStepErrorClassifier"/>. Treats configuration-class failures
/// (validation, configuration errors, authentication, user cancellation) as terminal,
/// and everything else as transient.
/// </summary>
/// <remarks>
/// The rationale: retrying a misconfigured step cannot make it succeed — the settings,
/// credentials, or cancellation decision won't change between attempts. Transient
/// categories (Timeout, ServiceUnavailable, RateLimiting, Unknown) may succeed on retry.
/// </remarks>
internal sealed class DefaultStepErrorClassifier : IStepErrorClassifier
{
    /// <inheritdoc />
    public bool IsTerminal(StepRunErrorCategory category)
        => category is StepRunErrorCategory.Validation
            or StepRunErrorCategory.ConfigurationError
            or StepRunErrorCategory.Authentication
            or StepRunErrorCategory.Cancelled;

    /// <inheritdoc />
    public StepRunErrorCategory Classify(Exception exception) => exception switch
    {
        ValidationException => StepRunErrorCategory.Validation,
        ArgumentException => StepRunErrorCategory.ConfigurationError,
        FormatException => StepRunErrorCategory.ConfigurationError,
        InvalidOperationException => StepRunErrorCategory.ConfigurationError,
        TimeoutException => StepRunErrorCategory.Timeout,
        OperationCanceledException => StepRunErrorCategory.Cancelled,
        UnauthorizedAccessException => StepRunErrorCategory.Authentication,
        _ => StepRunErrorCategory.Unknown,
    };
}
