using System.Runtime.CompilerServices;
using Umbraco.Automate.Core.Models;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.Extensions;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Deploy.Infrastructure.Artifacts;
using Umbraco.Deploy.Infrastructure.Connectors.ServiceConnectors;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Base class for Umbraco.Automate entity service connectors.
/// Provides common functionality for deploying Automate entities (Connections, Workspaces, Automations).
/// </summary>
public abstract class UmbracoAutomateEntityServiceConnectorBase<TArtifact, TEntity>(
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : ServiceConnectorBase<TArtifact, GuidUdi, TEntity>
    where TArtifact : DeployArtifactBase<GuidUdi>
    where TEntity : IAutomateEntity
{
    /// <summary>
    /// Accessor for retrieving deployment settings.
    /// </summary>
    protected readonly UmbracoAutomateDeploySettingsAccessor _settingsAccessor = settingsAccessor;

    /// <summary>
    /// The entity type associated with this connector, used for constructing UDIs.
    /// </summary>
    public abstract string UdiEntityType { get; }

    /// <summary>
    /// The container ID for open UDI ranges.
    /// </summary>
    public virtual string ContainerId => "-1";

    /// <summary>
    /// Gets the display name of an entity.
    /// </summary>
    public abstract string GetEntityName(TEntity entity);

    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// </summary>
    public abstract Task<TEntity?> GetEntityAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all entities of the relevant type.
    /// </summary>
    public abstract IAsyncEnumerable<TEntity> GetEntitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Constructs a deploy artifact for the specified entity.
    /// </summary>
    public abstract Task<TArtifact?> GetArtifactAsync(GuidUdi udi, TEntity? entity, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public override Task<TArtifact> GetArtifactAsync(
        TEntity entity,
        IContextCache contextCache,
        CancellationToken cancellationToken = default)
        => GetArtifactAsync(entity.GetUdi(UdiEntityType), entity, cancellationToken)!;

    /// <inheritdoc />
    public override async Task<TArtifact?> GetArtifactAsync(
        GuidUdi udi,
        IContextCache contextCache,
        CancellationToken cancellationToken = default)
    {
        EnsureType(udi);
        TEntity? entity = await GetEntityAsync(udi.Guid, cancellationToken).ConfigureAwait(false);
        return entity == null ? null : await GetArtifactAsync(udi, entity, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<NamedUdiRange> GetRangeAsync(
        GuidUdi udi,
        string selector,
        CancellationToken cancellationToken = default)
    {
        EnsureType(udi);

        if (udi.IsRoot)
        {
            EnsureSelector(udi, selector);
            return new NamedUdiRange(udi, OpenUdiName, selector);
        }

        TEntity? entity = await GetEntityAsync(udi.Guid, cancellationToken).ConfigureAwait(false);

        if (entity == null)
        {
            throw new ArgumentException("Could not find an entity with the specified identifier.", nameof(udi));
        }

        return GetRange(entity, selector);
    }

    /// <inheritdoc />
    public override async Task<NamedUdiRange> GetRangeAsync(
        string entityType,
        string sid,
        string selector,
        CancellationToken cancellationToken = default)
    {
        if (sid == "-1" || sid == ContainerId)
        {
            EnsureOpenSelector(selector);
            return new NamedUdiRange(Udi.Create(UdiEntityType), OpenUdiName, selector);
        }

        if (!Guid.TryParse(sid, out Guid result))
        {
            throw new ArgumentException("Invalid identifier.", nameof(sid));
        }

        TEntity? entity = await GetEntityAsync(result, cancellationToken).ConfigureAwait(false);

        if (entity == null)
        {
            throw new ArgumentException("Could not find an entity with the specified identifier.", nameof(sid));
        }

        return GetRange(entity, selector);
    }

    private NamedUdiRange GetRange(TEntity e, string selector)
        => new(e.GetUdi(UdiEntityType), GetEntityName(e), selector);

    /// <inheritdoc />
    public override async IAsyncEnumerable<GuidUdi> ExpandRangeAsync(
        UdiRange range,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureType(range.Udi);

        if (range.Udi.IsRoot)
        {
            EnsureSelector(range.Udi, range.Selector);

            await foreach (TEntity entity in GetEntitiesAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return entity.GetUdi(UdiEntityType);
            }
        }
        else
        {
            TEntity? entity = await GetEntityAsync(((GuidUdi)range.Udi).Guid, cancellationToken).ConfigureAwait(false);

            if (entity == null)
            {
                yield break;
            }

            if (range.Selector != "this")
            {
                throw new NotSupportedException("Unexpected selector \"" + range.Selector + "\".");
            }

            yield return entity.GetUdi(UdiEntityType);
        }
    }

    /// <inheritdoc />
    public override async Task<ArtifactDeployState<TArtifact, TEntity>> ProcessInitAsync(
        TArtifact artifact,
        IDeployContext context,
        CancellationToken cancellationToken = default)
    {
        EnsureType(artifact.Udi);

        TEntity? entity = await GetEntityAsync(artifact.Udi.Guid, cancellationToken).ConfigureAwait(false);

        return ArtifactDeployState.Create(artifact, entity, this, ProcessPasses[0]);
    }
}
