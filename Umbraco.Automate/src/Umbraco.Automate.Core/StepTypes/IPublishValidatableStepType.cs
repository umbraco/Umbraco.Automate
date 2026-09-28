using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Implemented by step types (actions, control flow) that need to check their resolved settings
/// before an automation is published. When an automation is published, the automation service calls
/// <see cref="ValidateSettingsForPublishAsync"/> for each step whose type implements this, and rejects
/// the publish if any errors are returned.
/// </summary>
/// <remarks>
/// <see cref="IValidatableStepType"/> runs on every draft save, so it should only reject values that
/// are malformed. This runs only on publish, so it is the place to require a setting to be filled in
/// or a referenced entity to exist — checks that would otherwise stop an author saving work in progress.
/// </remarks>
public interface IPublishValidatableStepType
{
    /// <summary>
    /// Validates the resolved settings for a step of an automation that is about to be published.
    /// Returns an empty list when valid, otherwise one message per problem.
    /// </summary>
    /// <param name="settings">The resolved settings instance (may be <c>null</c>).</param>
    /// <param name="automation">The automation being published, which owns the step.</param>
    /// <param name="cancellationToken">A token to cancel the validation.</param>
    Task<IReadOnlyList<string>> ValidateSettingsForPublishAsync(
        object? settings,
        Automation automation,
        CancellationToken cancellationToken = default);
}
