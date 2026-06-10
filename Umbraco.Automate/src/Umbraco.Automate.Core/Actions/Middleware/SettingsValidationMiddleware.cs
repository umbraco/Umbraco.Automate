using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Middleware that validates action settings against data annotation attributes
/// (e.g. <see cref="RequiredAttribute"/>) before the action executes.
/// Returns a failed result with <see cref="StepRunErrorCategory.Validation"/> if invalid.
/// </summary>
internal sealed class SettingsValidationMiddleware : IActionMiddleware
{
    private readonly ILogger<SettingsValidationMiddleware> _logger;

    public SettingsValidationMiddleware(ILogger<SettingsValidationMiddleware> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ActionResult> ApplyAsync(ActionContext context, ActionMiddlewareDelegate next, CancellationToken cancellationToken)
    {
        if (context.Settings is not null)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(context.Settings);

            if (!Validator.TryValidateObject(context.Settings, validationContext, validationResults, validateAllProperties: true))
            {
                var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));

                _logger.LogWarning(
                    "Settings validation failed for action '{ActionAlias}' in step {StepId}, run {RunId}: {Errors}",
                    context.ActionAlias,
                    context.StepId,
                    context.RunId,
                    errors);

                return Task.FromResult(ActionResult.Failed(
                    new ValidationException($"Settings validation failed: {errors}"),
                    StepRunErrorCategory.Validation));
            }
        }

        return next(context, cancellationToken);
    }
}
