using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class CreateMediaActionTests
{
    private readonly Mock<IMediaService> _mediaService = new();
    private readonly Mock<IMediaTypeService> _mediaTypeService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly Mock<IMediaFileDownloader> _fileDownloader = new();
    private readonly CreateMediaAction _action;

    public CreateMediaActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _authorizer
            .Setup(a => a.AuthorizeMediaRootAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _fileDownloader.SetupGet(d => d.DefaultFilePropertyAlias).Returns("umbracoFile");

        _action = new CreateMediaAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _mediaService.Object,
            _mediaTypeService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            _authorizer.Object,
            _fileDownloader.Object,
            Mock.Of<ILogger<CreateMediaAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.createMedia");

    [Fact]
    public async Task ExecuteAsync_EmptyMediaType_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = "",
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MediaTypeIsNotAKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = "Image",
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = Guid.NewGuid().ToString(),
            Name = "",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParentKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = "not-a-guid",
            MediaType = Guid.NewGuid().ToString(),
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_NoParent_CreatesAtTheMediaRoot()
    {
        var mediaTypeKey = Guid.NewGuid();
        var mediaKey = Guid.NewGuid();

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Key).Returns(mediaKey);
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", Constants.System.Root, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = null,
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        // The root is not a node, so it must not be looked up or authorised as one.
        _mediaService.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        _authorizer.Verify(a => a.AuthorizeMediaRootAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NoParentAndRootDenied_FailsWithAuthenticationError()
    {
        _authorizer
            .Setup(a => a.AuthorizeMediaRootAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Service account is confined to a start node."));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = "",
                MediaType = Guid.NewGuid().ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_ReturnsParentNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns((IMedia?)null);

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeParentNotFound);

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.MediaTypeKey.ShouldBe(mediaTypeKey);
    }

    [Fact]
    public async Task ExecuteAsync_MediaTypeNotFound_ReturnsMediaTypeNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns((IMediaType?)null);

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeMediaTypeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesAndSavesMedia()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        var mediaKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Key).Returns(mediaKey);
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(mediaKey);
        output.Name.ShouldBe("New Image");
        output.MediaTypeKey.ShouldBe(mediaTypeKey);
        output.MediaTypeAlias.ShouldBe("Image");
        output.ParentKey.ShouldBe(parentKey);
    }

    [Fact]
    public async Task ExecuteAsync_PickerValueWithTrailingComma_ResolvesTheMediaType()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = $"{mediaTypeKey},",
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NoSourceUrl_DoesNotDownloadAnything()
    {
        var setup = ArrangeCreatableMedia();

        var result = await _action.ExecuteAsync(setup.Context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        _fileDownloader.Verify(
            d => d.DownloadToPropertyAsync(It.IsAny<IMedia>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SourceUrl_StoresFileAndReportsFileName()
    {
        var setup = ArrangeCreatableMedia(sourceUrl: "https://static.tvmaze.com/uploads/images/medium_portrait/610/1525272.jpg");

        _fileDownloader
            .Setup(d => d.DownloadToPropertyAsync(setup.Created, setup.SourceUrl!, "umbracoFile", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediaFileDownloadResult.Succeeded("1525272.jpg"));

        var result = await _action.ExecuteAsync(setup.Context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.FileName.ShouldBe("1525272.jpg");
    }

    [Fact]
    public async Task ExecuteAsync_DownloadFails_StillCreatesItemWithSoftOutcome()
    {
        var setup = ArrangeCreatableMedia(sourceUrl: "https://example.com/gone.jpg");

        _fileDownloader
            .Setup(d => d.DownloadToPropertyAsync(setup.Created, setup.SourceUrl!, "umbracoFile", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MediaFileDownloadResult.Failed("HTTP 404"));

        var result = await _action.ExecuteAsync(setup.Context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeFileDownloadFailed);

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(setup.MediaKey);
        output.FileName.ShouldBeNull();

        // The item is still saved — one unreachable image must not abort a run looping over many.
        _mediaService.Verify(x => x.Save(setup.Created, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_MediaTypeHasNoFileProperty_ReturnsSoftOutcomeWithoutDownloading()
    {
        var setup = ArrangeCreatableMedia(sourceUrl: "https://example.com/photo.jpg", withFileProperty: false);

        var result = await _action.ExecuteAsync(setup.Context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeFileDownloadFailed);

        _fileDownloader.Verify(
            d => d.DownloadToPropertyAsync(It.IsAny<IMedia>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Fail(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    /// <summary>
    /// Arranges the happy path: a resolvable parent and invariant media type, a creatable item
    /// that saves successfully, and a context ready to execute.
    /// </summary>
    private MediaSetup ArrangeCreatableMedia(string? sourceUrl = null, bool withFileProperty = true)
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        var mediaKey = Guid.NewGuid();

        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var createdContentType = new Mock<ISimpleContentType>();
        createdContentType.SetupGet(x => x.Alias).Returns("Image");
        createdContentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Key).Returns(mediaKey);
        created.SetupGet(x => x.ContentType).Returns(createdContentType.Object);
        created.SetupGet(x => x.Properties).Returns(
            withFileProperty ? PropertiesWith("umbracoFile") : new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
                SourceUrl = sourceUrl,
            },
            Guid.NewGuid());

        return new MediaSetup(context, created.Object, mediaKey, sourceUrl);
    }

    private static PropertyCollection PropertiesWith(params string[] aliases)
        => new(aliases.Select(alias =>
        {
            var propertyType = new Mock<IPropertyType>();
            propertyType.SetupGet(x => x.Alias).Returns(alias);
            propertyType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
            return new Property(propertyType.Object);
        }));

    private sealed record MediaSetup(ActionContext Context, IMedia Created, Guid MediaKey, string? SourceUrl);

    private static ActionContext CreateContext(CreateMediaSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.createMedia",
            Settings = settings,
            ExecutionContext = serviceAccountKey.HasValue
                ? new AutomationExecutionContext
                {
                    ServiceAccountKey = serviceAccountKey.Value,
                    WorkspaceId = Guid.NewGuid(),
                    WorkspaceName = "Test",
                    AutomationId = Guid.NewGuid(),
                    AutomationName = "Test",
                    RunId = Guid.NewGuid(),
                    InitiatorType = "test",
                    AllowedConnections = [],
                }
                : null,
        };
}
