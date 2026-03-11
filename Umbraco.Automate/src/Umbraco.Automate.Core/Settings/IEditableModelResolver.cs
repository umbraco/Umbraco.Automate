namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Service for resolving editable models from various storage formats.
/// Handles JSON deserialization, configuration variable substitution, and validation.
/// </summary>
public interface IEditableModelResolver
{
    /// <summary>
    /// Resolves a model from storage format (JsonElement, Dictionary, etc.) to a typed instance.
    /// Supports configuration variable substitution using the pattern: $ConfigKey.
    /// Validates the resolved model using the schema's validation rules.
    /// </summary>
    /// <typeparam name="TModel">The type of model to resolve to.</typeparam>
    /// <param name="modelId">The model ID for validation context (action alias or trigger alias).</param>
    /// <param name="data">The data object to resolve (can be JsonElement, typed object, or null).</param>
    /// <param name="schema">Optional schema for validation. When null, validation is skipped.</param>
    /// <returns>Typed model instance, or null if data parameter was null.</returns>
    TModel? ResolveModel<TModel>(string modelId, object? data, EditableModelSchema? schema = null)
        where TModel : class, new();

    /// <summary>
    /// Resolves a model from storage format to a typed instance using a runtime <see cref="Type"/>.
    /// Use this overload when the target type is not known at compile time.
    /// </summary>
    /// <param name="modelId">The model ID for validation context (action alias or trigger alias).</param>
    /// <param name="modelType">The target type to deserialize to.</param>
    /// <param name="data">The data object to resolve (can be JsonElement, typed object, or null).</param>
    /// <param name="schema">Optional schema for validation. When null, validation is skipped.</param>
    /// <returns>Typed model instance, or null if data parameter was null.</returns>
    object? ResolveModel(string modelId, Type modelType, object? data, EditableModelSchema? schema = null);
}
