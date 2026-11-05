namespace Umbraco.Automate.Core.Persistence.Scoping;

/// <summary>
/// Records whether the DI-registered pooled factory for <typeparamref name="TDbContext"/> built its
/// options on <see cref="DatabaseConnectionInfo.PlaceholderConnection"/>.
/// </summary>
/// <remarks>
/// Set explicitly by the options delegate at the moment it falls back, rather than inferred later from
/// the built options' connection string, which would tie the check to EF Core internals. One instance
/// per context type, so Automate and OpenIddict never see each other's state.
/// </remarks>
/// <typeparam name="TDbContext">The context the state belongs to.</typeparam>
internal sealed class PlaceholderConnectionState<TDbContext>
{
    private volatile bool _builtOnPlaceholder;

    /// <summary>
    /// Whether the DI pool was built on the placeholder connection.
    /// </summary>
    public bool BuiltOnPlaceholder => _builtOnPlaceholder;

    /// <summary>
    /// Marks the DI pool as built on the placeholder connection.
    /// </summary>
    public void MarkBuiltOnPlaceholder() => _builtOnPlaceholder = true;
}
