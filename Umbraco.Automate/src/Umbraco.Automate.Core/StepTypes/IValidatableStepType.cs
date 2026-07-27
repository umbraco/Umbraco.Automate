namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Implemented by step types (actions, control flow) that can validate their own resolved settings
/// at author time. When an automation is saved, the automation service calls
/// <see cref="ValidateSettingsAsync"/> for each step whose type implements this, and rejects the
/// save if any errors are returned.
/// </summary>
public interface IValidatableStepType
{
    /// <summary>
    /// Validates the resolved settings for a step. Returns an empty list when valid, otherwise one
    /// message per problem.
    /// </summary>
    /// <param name="settings">The resolved settings instance (may be <c>null</c>).</param>
    /// <param name="cancellationToken">A token to cancel the validation.</param>
    Task<IReadOnlyList<string>> ValidateSettingsAsync(object? settings, CancellationToken cancellationToken = default);
}
