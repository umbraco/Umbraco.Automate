// Shared arrange for the specs of docs/plans/537-create-via-editing-services (#537).
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentEditing.Validation;
using Umbraco.Cms.Core.Models.TemporaryFile;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Builds the action the way Umbraco's DI does (<see cref="ActivatorUtilities"/>), from a container
/// that holds both the released constructors' services and the editing services, so the specs
/// compile against either constructor and run against the one DI picks.
/// </summary>
internal static class CreateActionSpecs
{
    public static IShortStringHelper ShortStringHelper()
    {
        var helper = new Mock<IShortStringHelper>();
        helper
            .Setup(x => x.CleanString(It.IsAny<string>(), It.IsAny<CleanStringType>()))
            .Returns<string, CleanStringType>((text, _) => text);
        return helper.Object;
    }

    public static IUmbracoContextFactory UmbracoContextFactory()
    {
        var factory = new Mock<IUmbracoContextFactory>();
        factory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));
        return factory.Object;
    }

    public static IUserIdKeyResolver UserIdKeyResolver()
    {
        var resolver = new Mock<IUserIdKeyResolver>();
        resolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);
        return resolver.Object;
    }

    public static ActionContext Context(string actionAlias, object settings, Guid serviceAccountKey)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = actionAlias,
            Settings = settings,
            ExecutionContext = new AutomationExecutionContext
            {
                ServiceAccountKey = serviceAccountKey,
                WorkspaceId = Guid.NewGuid(),
                WorkspaceName = "Test",
                AutomationId = Guid.NewGuid(),
                AutomationName = "Test",
                RunId = Guid.NewGuid(),
                InitiatorType = "test",
                AllowedConnections = [],
            },
        };

    /// <summary>
    /// Reads an output property by name, so specs can name output fields the build adds.
    /// </summary>
    public static object? Output(ActionResult result, string propertyName)
        => result.OutputData?.GetType().GetProperty(propertyName)?.GetValue(result.OutputData);

    /// <summary>Serializes a value sent to the CMS, whatever CLR shape it has.</summary>
    public static string Json(object? value) => JsonSerializer.Serialize(value);

    /// <summary>Reads one field of a value sent to the CMS, ignoring the property name's case.</summary>
    public static string? JsonField(object? value, string field)
    {
        using var document = JsonDocument.Parse(Json(value));
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
            }
        }

        return null;
    }

    public static PropertyValidationError Error(string alias, string? culture = null)
        => new()
        {
            Alias = alias,
            Culture = culture,
            Segment = null,
            JsonPath = string.Empty,
            ErrorMessages = ["Value is invalid"],
        };
}

/// <summary>Arrange for Create Content: an invariant (or culture-variant) "page" type under a parent.</summary>
internal sealed class CreateContentHarness
{
    private readonly IShortStringHelper _shortStringHelper = CreateActionSpecs.ShortStringHelper();
    private readonly ContentType _contentType;

    public CreateContentHarness(ContentVariation variations = ContentVariation.Nothing)
    {
        _contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = "page",
            Key = ContentTypeKey,
            Variations = variations,
            AllowedAsRoot = true,
        };
        ContentTypeService.Setup(x => x.Get(ContentTypeKey)).Returns(_contentType);

        Authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);
        Authorizer
            .Setup(a => a.AuthorizeContentRootAsync(It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        CreateReturns(ContentEditingOperationStatus.Success);
    }

    public Guid ContentTypeKey { get; } = Guid.NewGuid();

    public Guid ParentKey { get; } = Guid.NewGuid();

    public Guid CreatedKey { get; } = Guid.NewGuid();

    public Guid ServiceAccountKey { get; } = Guid.NewGuid();

    public Mock<IContentEditingService> EditingService { get; } = new();

    public Mock<IContentTypeService> ContentTypeService { get; } = new();

    public Mock<IAutomationActionAuthorizer> Authorizer { get; } = new();

    /// <summary>The model the action last passed to <c>CreateAsync</c>.</summary>
    public ContentCreateModel? Model { get; private set; }

    public CreateContentHarness WithProperty(
        string alias,
        string editorAlias = Constants.PropertyEditors.Aliases.TextBox,
        ContentVariation variations = ContentVariation.Nothing)
    {
        _contentType.AddPropertyType(new PropertyType(_shortStringHelper, editorAlias, ValueStorageType.Nvarchar, alias)
        {
            Variations = variations,
        });
        return this;
    }

    /// <summary>
    /// Sets what <c>CreateAsync</c> answers. Like the CMS, a <c>PropertyValidationError</c> still
    /// saves the content (<c>ContentEditingService.cs:153-158</c> at release-18.1.0).
    /// </summary>
    public void CreateReturns(ContentEditingOperationStatus status, params PropertyValidationError[] errors)
    {
        var saved = status is ContentEditingOperationStatus.Success or ContentEditingOperationStatus.PropertyValidationError;
        var result = new ContentCreateResult
        {
            Content = saved ? Mock.Of<IContent>(c => c.Key == CreatedKey && c.Name == "New Page") : null,
            ValidationResult = new ContentValidationResult { ValidationErrors = errors },
        };

        var attempt = saved
            ? Attempt.SucceedWithStatus(status, result)
            : Attempt.FailWithStatus(status, result);

        EditingService
            .Setup(x => x.CreateAsync(It.IsAny<ContentCreateModel>(), It.IsAny<Guid>()))
            .Callback<ContentCreateModel, Guid>((model, _) => Model = model)
            .ReturnsAsync(attempt);
    }

    public CreateContentSettings Settings(string? propertiesJson = null, bool atRoot = false, string? culture = null)
        => new()
        {
            ParentKey = atRoot ? string.Empty : ParentKey.ToString(),
            ContentType = ContentTypeKey.ToString(),
            Name = "New Page",
            Culture = culture,
            PropertiesJson = propertiesJson,
        };

    public CreateContentAction BuildAction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));
        services.AddSingleton(EditingService.Object);
        services.AddSingleton(ContentTypeService.Object);
        services.AddSingleton(Authorizer.Object);
        services.AddSingleton(Mock.Of<IContentService>());
        services.AddSingleton(CreateActionSpecs.UserIdKeyResolver());
        services.AddSingleton(Mock.Of<IBackOfficeSecurityAccessor>());
        services.AddSingleton(CreateActionSpecs.UmbracoContextFactory());

        return ActivatorUtilities.CreateInstance<CreateContentAction>(services.BuildServiceProvider());
    }

    public Task<ActionResult> RunAsync(CreateContentSettings settings)
        => BuildAction().ExecuteAsync(
            CreateActionSpecs.Context("umbracoAutomate.createContent", settings, ServiceAccountKey),
            CancellationToken.None);

    /// <summary>The value the action sent for <paramref name="alias"/>, or <c>null</c> when it sent none.</summary>
    public PropertyValueModel? Value(string alias) => Model?.Properties.SingleOrDefault(p => p.Alias == alias);
}

/// <summary>
/// Arrange for Create Media: an "Image" type with an <c>umbracoFile</c> Image Cropper property, a
/// real <see cref="MediaFileDownloader"/> over a stubbed HTTP response, and a mocked CMS
/// <see cref="ITemporaryFileService"/>.
/// </summary>
internal sealed class CreateMediaHarness
{
    public const string ImageUrl = "https://images.example.com/photos/sunset.jpg";

    private readonly IShortStringHelper _shortStringHelper = CreateActionSpecs.ShortStringHelper();
    private readonly MediaType _mediaType;
    private HttpStatusCode _downloadStatus = HttpStatusCode.OK;

    public CreateMediaHarness(bool withFileProperty = true)
    {
        _mediaType = new MediaType(_shortStringHelper, -1)
        {
            Alias = "Image",
            Key = MediaTypeKey,
            AllowedAsRoot = true,
        };

        if (withFileProperty)
        {
            WithProperty(Constants.Conventions.Media.File, Constants.PropertyEditors.Aliases.ImageCropper);
        }

        MediaTypeService.Setup(x => x.Get(MediaTypeKey)).Returns(_mediaType);

        Authorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);
        Authorizer
            .Setup(a => a.AuthorizeMediaRootAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        TemporaryFileService
            .Setup(x => x.CreateAsync(It.IsAny<CreateTemporaryFileModel>()))
            .Callback<CreateTemporaryFileModel>(model => TemporaryFile = model)
            .ReturnsAsync((CreateTemporaryFileModel model) =>
                Attempt.SucceedWithStatus<TemporaryFileModel?, TemporaryFileOperationStatus>(
                    TemporaryFileOperationStatus.Success,
                    new TemporaryFileModel
                    {
                        Key = model.Key,
                        FileName = model.FileName,
                        OpenReadStream = model.OpenReadStream,
                        AvailableUntil = DateTime.Now.AddDays(1),
                    }));

        ValidateReturns(ContentEditingOperationStatus.Success);
        CreateReturns(ContentEditingOperationStatus.Success);
    }

    public Guid MediaTypeKey { get; } = Guid.NewGuid();

    public Guid ParentKey { get; } = Guid.NewGuid();

    public Guid CreatedKey { get; } = Guid.NewGuid();

    public Guid ServiceAccountKey { get; } = Guid.NewGuid();

    public Mock<IMediaEditingService> EditingService { get; } = new();

    public Mock<IMediaTypeService> MediaTypeService { get; } = new();

    public Mock<IAutomationActionAuthorizer> Authorizer { get; } = new();

    public Mock<ITemporaryFileService> TemporaryFileService { get; } = new();

    /// <summary>The model the action last passed to <c>CreateAsync</c>.</summary>
    public MediaCreateModel? Model { get; private set; }

    /// <summary>The temporary file the download last asked the CMS to create.</summary>
    public CreateTemporaryFileModel? TemporaryFile { get; private set; }

    public CreateMediaHarness WithProperty(string alias, string editorAlias = Constants.PropertyEditors.Aliases.TextBox)
    {
        _mediaType.AddPropertyType(new PropertyType(_shortStringHelper, editorAlias, ValueStorageType.Nvarchar, alias));
        return this;
    }

    public CreateMediaHarness DownloadReturns(HttpStatusCode status)
    {
        _downloadStatus = status;
        return this;
    }

    public void TemporaryFileRefused(TemporaryFileOperationStatus status)
        => TemporaryFileService
            .Setup(x => x.CreateAsync(It.IsAny<CreateTemporaryFileModel>()))
            .ReturnsAsync(Attempt.FailWithStatus<TemporaryFileModel?, TemporaryFileOperationStatus>(status, null));

    public void ValidateReturns(ContentEditingOperationStatus status, params PropertyValidationError[] errors)
    {
        var result = new ContentValidationResult { ValidationErrors = errors };
        var attempt = status == ContentEditingOperationStatus.Success
            ? Attempt.SucceedWithStatus(status, result)
            : Attempt.FailWithStatus(status, result);

        EditingService
            .Setup(x => x.ValidateCreateAsync(It.IsAny<MediaCreateModel>()))
            .ReturnsAsync(attempt);
    }

    /// <summary>
    /// Sets what <c>CreateAsync</c> answers. Like the CMS, media is never saved with a property
    /// error (<c>MediaEditingService.cs:114-118</c> at release-18.1.0).
    /// </summary>
    public void CreateReturns(ContentEditingOperationStatus status, params PropertyValidationError[] errors)
    {
        var saved = status == ContentEditingOperationStatus.Success;
        var result = new MediaCreateResult
        {
            Content = saved ? Mock.Of<IMedia>(m => m.Key == CreatedKey && m.Name == "Sunset") : null,
            ValidationResult = new ContentValidationResult { ValidationErrors = errors },
        };

        var attempt = saved
            ? Attempt.SucceedWithStatus(status, result)
            : Attempt.FailWithStatus(status, result);

        EditingService
            .Setup(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()))
            .Callback<MediaCreateModel, Guid>((model, _) => Model = model)
            .ReturnsAsync(attempt);
    }

    public CreateMediaSettings Settings(string? sourceUrl = null, string? propertiesJson = null, bool atRoot = false)
        => new()
        {
            ParentKey = atRoot ? string.Empty : ParentKey.ToString(),
            MediaType = MediaTypeKey.ToString(),
            Name = "Sunset",
            SourceUrl = sourceUrl,
            PropertiesJson = propertiesJson,
        };

    public CreateMediaAction BuildAction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new ActionInfrastructure(Mock.Of<IEditableModelResolver>()));
        services.AddSingleton(EditingService.Object);
        services.AddSingleton(MediaTypeService.Object);
        services.AddSingleton(Authorizer.Object);
        services.AddSingleton(TemporaryFileService.Object);
        services.AddSingleton(Mock.Of<IMediaService>());
        services.AddSingleton(CreateActionSpecs.UserIdKeyResolver());
        services.AddSingleton(Mock.Of<IBackOfficeSecurityAccessor>());
        services.AddSingleton(CreateActionSpecs.UmbracoContextFactory());

        // The real downloader, over a stubbed response, with every service either of its
        // constructors may ask for.
        services.AddSingleton(HttpClientFactory());
        services.AddSingleton(_shortStringHelper);
        services.AddSingleton(new MediaFileManager(
            Mock.Of<IFileSystem>(),
            Mock.Of<IMediaPathScheme>(),
            NullLogger<MediaFileManager>.Instance,
            _shortStringHelper,
            Mock.Of<IServiceProvider>(),
            new Lazy<ICoreScopeProvider>(Mock.Of<ICoreScopeProvider>)));
        services.AddSingleton(new MediaUrlGeneratorCollection(() => []));
        services.AddSingleton(Mock.Of<IContentTypeBaseServiceProvider>());
        services.AddSingleton(Options.Create(new ExecutionOptions()));
        services.AddSingleton<IMediaFileDownloader>(sp => ActivatorUtilities.CreateInstance<MediaFileDownloader>(sp));

        return ActivatorUtilities.CreateInstance<CreateMediaAction>(services.BuildServiceProvider());
    }

    public Task<ActionResult> RunAsync(CreateMediaSettings settings)
        => BuildAction().ExecuteAsync(
            CreateActionSpecs.Context("umbracoAutomate.createMedia", settings, ServiceAccountKey),
            CancellationToken.None);

    /// <summary>The value the action sent for <paramref name="alias"/>, or <c>null</c> when it sent none.</summary>
    public PropertyValueModel? Value(string alias) => Model?.Properties.SingleOrDefault(p => p.Alias == alias);

    private IHttpClientFactory HttpClientFactory()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new StubHandler(() => _downloadStatus)));
        return factory.Object;
    }

    private sealed class StubHandler(Func<HttpStatusCode> status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status());
            if (response.IsSuccessStatusCode)
            {
                response.Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            }

            return Task.FromResult(response);
        }
    }
}
