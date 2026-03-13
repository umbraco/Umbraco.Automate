using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Models;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Web.Api.Management.Versioning.Models;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Controllers;

/// <summary>
/// Unified controller for entity version history operations.
/// </summary>
[ApiVersion("1.0")]
public class EntityVersionHistoryController : VersioningControllerBase
{
    private readonly IEntityVersionService _versionService;
    private readonly VersionableEntityAdapterCollection _entityTypes;
    private readonly IUmbracoMapper _umbracoMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityVersionHistoryController"/> class.
    /// </summary>
    public EntityVersionHistoryController(
        IEntityVersionService versionService,
        VersionableEntityAdapterCollection entityTypes,
        IUmbracoMapper umbracoMapper)
    {
        _versionService = versionService;
        _entityTypes = entityTypes;
        _umbracoMapper = umbracoMapper;
    }

    /// <summary>
    /// Get version history for an entity.
    /// </summary>
    [HttpGet($"{{{nameof(entityType)}}}/{{{nameof(entityId)}}}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(EntityVersionHistoryResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionHistory(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var handler = _entityTypes.GetByTypeName(entityType);
        if (handler is null)
        {
            return UnknownEntityType(entityType);
        }

        var currentEntity = await handler.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is not IAuditableEntity auditable)
        {
            return EntityNotFound(entityType, entityId);
        }

        var currentVersionModel = new EntityVersionResponseModel
        {
            Id = auditable.Id,
            EntityId = entityId,
            Version = currentEntity is IVersionableEntity versionable ? versionable.Version : 1,
            DateCreated = auditable.DateModified,
            CreatedByUserId = auditable.ModifiedByUserId,
        };

        // Current version occupies position 0, so adjust skip/take for historical query.
        var includeCurrentVersion = skip == 0;
        var historicalSkip = skip > 0 ? skip - 1 : 0;
        var historicalTake = includeCurrentVersion ? Math.Max(0, take - 1) : take;

        var (historicalVersions, historicalTotal) = await _versionService.GetVersionHistoryAsync(
            entityId,
            handler.EntityTypeName,
            historicalSkip,
            historicalTake,
            cancellationToken);

        var pagedVersions = new List<EntityVersionResponseModel>();

        if (includeCurrentVersion)
        {
            pagedVersions.Add(currentVersionModel);
        }

        pagedVersions.AddRange(historicalVersions.Select(v => _umbracoMapper.Map<EntityVersionResponseModel>(v)!));

        var totalVersions = historicalTotal + 1;

        return Ok(new EntityVersionHistoryResponseModel
        {
            CurrentVersion = currentVersionModel.Version,
            TotalVersions = totalVersions,
            Versions = pagedVersions,
        });
    }

    /// <summary>
    /// Get a specific version snapshot for an entity.
    /// </summary>
    [HttpGet($"{{{nameof(entityType)}}}/{{{nameof(entityId)}}}/{{{nameof(entityVersion)}}}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(EntityVersionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        [FromRoute] int entityVersion,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeEntityType(entityType);
        if (normalizedType is null)
        {
            return UnknownEntityType(entityType);
        }

        var versionRecord = await _versionService.GetVersionAsync(entityId, normalizedType, entityVersion, cancellationToken);
        if (versionRecord is null)
        {
            return VersionNotFound(entityVersion);
        }

        return Ok(_umbracoMapper.Map<EntityVersionResponseModel>(versionRecord));
    }

    /// <summary>
    /// Compare two versions of an entity.
    /// </summary>
    [HttpGet($"{{{nameof(entityType)}}}/{{{nameof(entityId)}}}/{{{nameof(fromEntityVersion)}}}/compare/{{{nameof(toEntityVersion)}}}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(EntityVersionComparisonResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompareVersions(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        [FromRoute] int fromEntityVersion,
        [FromRoute] int toEntityVersion,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = NormalizeEntityType(entityType);
        if (normalizedType is null)
        {
            return UnknownEntityType(entityType);
        }

        var comparison = await _versionService.CompareVersionsAsync(
            entityId, normalizedType, fromEntityVersion, toEntityVersion, cancellationToken);

        if (comparison is null)
        {
            return VersionNotFound(fromEntityVersion);
        }

        return Ok(new EntityVersionComparisonResponseModel
        {
            FromVersion = comparison.FromVersion,
            ToVersion = comparison.ToVersion,
            Changes = comparison.Changes.Select(c => new ValueChangeModel
            {
                Path = c.Path,
                OldValue = c.OldValue,
                NewValue = c.NewValue,
            }).ToList(),
        });
    }

    /// <summary>
    /// Gets the list of supported entity types.
    /// </summary>
    [HttpGet("supported-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public IActionResult GetSupportedEntityTypes()
    {
        var types = _entityTypes.Select(a => a.EntityTypeName);
        return Ok(types);
    }

    /// <summary>
    /// Rollback an entity to a previous version.
    /// </summary>
    [HttpPost($"{{{nameof(entityType)}}}/{{{nameof(entityId)}}}/{{{nameof(entityVersion)}}}/rollback")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RollbackToVersion(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        [FromRoute] int entityVersion,
        CancellationToken cancellationToken = default)
    {
        var handler = _entityTypes.GetByTypeName(entityType);
        if (handler is null)
        {
            return UnknownEntityType(entityType);
        }

        try
        {
            await handler.RollbackAsync(entityId, entityVersion, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ProblemDetailsBuilder()
                .WithTitle("Rollback failed")
                .WithDetail(ex.Message)
                .Build());
        }
    }

    private string? NormalizeEntityType(string entityType)
    {
        var handler = _entityTypes.GetByTypeName(entityType);
        return handler?.EntityTypeName;
    }
}
