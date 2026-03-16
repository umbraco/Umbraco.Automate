using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Models;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Web.Api.Management.Versioning.Models;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Web.Api.Management.Versioning.Controllers;

/// <summary>
/// Unified controller for entity version history operations.
/// </summary>
[ApiVersion("1.0")]
public class EntityVersionHistoryController : VersioningControllerBase
{
    private readonly IEntityVersionService _versionService;
    private readonly VersionableEntityAdapterCollection _entityTypes;
    private readonly IAuthorizationService _authorizationService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoMapper _umbracoMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityVersionHistoryController"/> class.
    /// </summary>
    public EntityVersionHistoryController(
        IEntityVersionService versionService,
        VersionableEntityAdapterCollection entityTypes,
        IAuthorizationService authorizationService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoMapper umbracoMapper)
    {
        _versionService = versionService;
        _entityTypes = entityTypes;
        _authorizationService = authorizationService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
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

        var forbidden = await AuthorizeEntityAccessAsync(currentEntity);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var currentVersion = currentEntity is IVersionableEntity versionable ? versionable.Version : 1;
        var publishedVersion = currentEntity is IPublishableEntity publishable ? publishable.PublishedVersion : null;

        var currentVersionModel = new EntityVersionResponseModel
        {
            Id = auditable.Id,
            EntityId = entityId,
            Version = currentVersion,
            DateCreated = auditable.DateModified,
            CreatedByUserId = auditable.ModifiedByUserId,
            IsPublished = publishedVersion == currentVersion,
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

        pagedVersions.AddRange(historicalVersions.Select(v =>
        {
            var model = _umbracoMapper.Map<EntityVersionResponseModel>(v)!;
            model.IsPublished = publishedVersion == model.Version;
            return model;
        }));

        var totalVersions = historicalTotal + 1;

        return Ok(new EntityVersionHistoryResponseModel
        {
            CurrentVersion = currentVersionModel.Version,
            PublishedVersion = publishedVersion,
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
        var handler = _entityTypes.GetByTypeName(entityType);
        if (handler is null)
        {
            return UnknownEntityType(entityType);
        }

        var currentEntity = await handler.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is null)
        {
            return EntityNotFound(entityType, entityId);
        }

        var forbidden = await AuthorizeEntityAccessAsync(currentEntity);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var versionRecord = await _versionService.GetVersionAsync(entityId, handler.EntityTypeName, entityVersion, cancellationToken);
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
        var handler = _entityTypes.GetByTypeName(entityType);
        if (handler is null)
        {
            return UnknownEntityType(entityType);
        }

        var currentEntity = await handler.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is null)
        {
            return EntityNotFound(entityType, entityId);
        }

        var forbidden = await AuthorizeEntityAccessAsync(currentEntity);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var comparison = await _versionService.CompareVersionsAsync(
            entityId, handler.EntityTypeName, fromEntityVersion, toEntityVersion, cancellationToken);

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

        var currentEntity = await handler.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is null)
        {
            return EntityNotFound(entityType, entityId);
        }

        var forbidden = await AuthorizeEntityAccessAsync(currentEntity);
        if (forbidden is not null)
        {
            return forbidden;
        }

        try
        {
            var userKey = CurrentUserKey(_backOfficeSecurityAccessor);
            await handler.RollbackAsync(entityId, entityVersion, userKey, cancellationToken);
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

    /// <summary>
    /// Resolves the workspace ID from the entity and authorizes workspace access.
    /// For automations, uses the WorkspaceId property. For workspaces, the entity ID is the workspace.
    /// </summary>
    private async Task<IActionResult?> AuthorizeEntityAccessAsync(object entity)
    {
        Guid? workspaceId = entity switch
        {
            Core.Automations.Automation automation => automation.WorkspaceId,
            Core.Workspaces.Workspace workspace => workspace.Id,
            _ => null,
        };

        if (workspaceId.HasValue)
        {
            return await AuthorizeWorkspaceAccessAsync(_authorizationService, workspaceId.Value);
        }

        return null;
    }
}
