using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Builders.Extensions;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;

namespace Umbraco.Automate.Tests.CmsIntegration;

/// <summary>
/// From CMS 19, publishing named cultures of culture-variant content only publishes the shared
/// (invariant) properties when one of the publishing user's groups allows editing shared content.
/// These tests run the real Publish Content action as a service account with and without it.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class PublishContentSharedPropertiesTests : UmbracoIntegrationTest
{
    private const string English = "en-US";
    private const string Danish = "da-DK";
    private static readonly Guid BlockKey = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // A fresh test database has no package migration state, so every test host runs Automate's package
    // migrations and the CMS start-up handlers in a background service after the host starts. On the
    // in-memory shared-cache SQLite the tests use, writes from a test that begins before that finishes
    // collide with it, and the CMS's SQLite retry policy then waits 30 s per attempt (about 9 minutes
    // in all). The routing readiness flag is set once that background work is done.
    [SetUp]
    public async Task WaitForStartupWorkAsync()
    {
        var readiness = GetRequiredService<IContentRoutingReadiness>();
        var runtimeState = GetRequiredService<IRuntimeState>();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!readiness.IsReady)
        {
            if (runtimeState.Level == RuntimeLevel.BootFailed)
            {
                Assert.Fail($"The test host failed to boot: {runtimeState.BootFailedException}");
            }

            Assert.That(DateTime.UtcNow, Is.LessThan(deadline), "The test host did not finish its start-up work in 60 seconds.");
            await Task.Delay(20);
        }
    }

    private IContentEditingService ContentEditingService => GetRequiredService<IContentEditingService>();

    private IContentService ContentService => GetRequiredService<IContentService>();

    private IContentTypeService ContentTypeService => GetRequiredService<IContentTypeService>();

    private ILanguageService LanguageService => GetRequiredService<ILanguageService>();

    private IUserService UserService => GetRequiredService<IUserService>();

    private IUserGroupService UserGroupService => GetRequiredService<IUserGroupService>();

    [Test]
    public async Task WithTheSharedContentPermission_PublishesTheSharedPropertyChange()
    {
        var contentKey = await CreatePublishedContentWithPendingChangesAsync();
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: true);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        var content = ContentService.GetById(contentKey)!;
        Assert.Multiple(() =>
        {
            Assert.That(content.GetValue<string>("variantValue", English, published: true), Is.EqualTo("Changed EN"));
            Assert.That(content.GetValue<string>("invariantValue", published: true), Is.EqualTo("Changed shared"));
            Assert.That(context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning), Is.Empty);
        });
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_LeavesTheSharedPropertyUnpublishedAndWarns()
    {
        var contentKey = await CreatePublishedContentWithPendingChangesAsync();
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        // The CMS reports success: the culture published, the shared property did not.
        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        var content = ContentService.GetById(contentKey)!;
        var warnings = context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(content.GetValue<string>("variantValue", English, published: true), Is.EqualTo("Changed EN"));
            Assert.That(content.GetValue<string>("invariantValue", published: true), Is.EqualTo("Initial shared"));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("invariantValue").And.Contain("shared content"));
        });
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_AndNoSharedChange_DoesNotWarn()
    {
        var contentKey = await CreatePublishedContentWithPendingChangesAsync(changeSharedValue: false);
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        Assert.That(context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_PendingBlocksOfAnotherCultureOnly_AreNotReported()
    {
        // A named-culture publish merges the structure and that culture's block values, so another
        // culture's pending blocks are expected, and nothing shared was held back.
        var contentKey = await CreateContentWithBlockListAsync(changeDanishBlockText: true);
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        Assert.That(context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_AnUnchangedBlockList_IsNotReported()
    {
        var contentKey = await CreateContentWithBlockListAsync();
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        Assert.That(context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_ASharedValueInsideABlock_IsReportedAsHeldBack()
    {
        var contentKey = await CreateContentWithBlockListAsync(changeDanishBlockText: true, changeBlockSharedValue: true);
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        var warnings = context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(ContentService.GetById(contentKey)!.GetValue<string>("blocks", published: true), Does.Not.Contain("Changed block shared"));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("(blocks)"));
        });
    }

    [Test]
    public async Task WithTheSharedContentPermission_ASharedValueInsideABlock_IsPublishedAndNotReported()
    {
        var contentKey = await CreateContentWithBlockListAsync(changeDanishBlockText: true, changeBlockSharedValue: true);
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: true);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        Assert.Multiple(() =>
        {
            Assert.That(ContentService.GetById(contentKey)!.GetValue<string>("blocks", published: true), Does.Contain("Changed block shared"));
            Assert.That(context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning), Is.Empty);
        });
    }

    [Test]
    public async Task WithoutTheSharedContentPermission_ASegmentOnlyPropertyChange_IsReportedAsHeldBack()
    {
        var contentKey = await CreateContentWithPendingSegmentValueAsync();
        var serviceAccount = await CreateServiceAccountAsync(hasAccessToInvariantForVariant: false);

        var (result, context) = await PublishAsync(contentKey, serviceAccount.Key, English);

        Assert.That(result.Status, Is.EqualTo(ActionResultStatus.Success));
        var content = ContentService.GetById(contentKey)!;
        var warnings = context.LogEntries.Where(e => e.Level == ActionLogLevel.Warning).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(content.GetValue<string>("segmentValue", culture: null, segment: "s1", published: true), Is.EqualTo("Initial segment"));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0].Message, Does.Contain("segmentValue"));
        });
    }

    private async Task<(ActionResult Result, ActionContext Context)> PublishAsync(Guid contentKey, Guid serviceAccountKey, string cultures)
    {
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var action = new PublishContentAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            GetRequiredService<IContentPublishingService>(),
            UserService,
            GetRequiredService<PropertyEditorCollection>(),
            GetRequiredService<IBackOfficeSecurityAccessor>(),
            GetRequiredService<IUmbracoContextFactory>(),
            authorizer.Object,
            NullLogger<PublishContentAction>.Instance);

        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.publishContent",
            Settings = new PublishContentSettings { ContentKey = contentKey.ToString(), Cultures = cultures },
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

        return (await action.ExecuteAsync(context, CancellationToken.None), context);
    }

    // Published in both cultures, then changed in English and (optionally) in the shared property.
    private async Task<Guid> CreatePublishedContentWithPendingChangesAsync(bool changeSharedValue = true)
    {
        await LanguageService.CreateAsync(new LanguageBuilder().WithCultureInfo(Danish).Build(), Constants.Security.SuperUserKey);

        var key = Guid.NewGuid();
        var contentType = new ContentTypeBuilder()
            .WithAlias("sharedPropertyContent")
            .WithName("Shared Property Content")
            .WithKey(key)
            .WithContentVariation(ContentVariation.Culture)
            .AddAllowedContentType()
            .WithKey(key)
            .WithAlias("sharedPropertyContent")
            .Done()
            .AddPropertyType()
            .WithAlias("variantValue")
            .WithVariations(ContentVariation.Culture)
            .Done()
            .AddPropertyType()
            .WithAlias("invariantValue")
            .WithVariations(ContentVariation.Nothing)
            .Done()
            .Build();
        contentType.AllowedAsRoot = true;
        await ContentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);

        var created = await ContentEditingService.CreateAndPublishAsync(
            new ContentCreateModel
            {
                ContentTypeKey = contentType.Key,
                ParentKey = Constants.System.RootKey,
                Variants =
                [
                    new VariantModel { Culture = English, Name = "English" },
                    new VariantModel { Culture = Danish, Name = "Danish" },
                ],
                Properties =
                [
                    new PropertyValueModel { Alias = "variantValue", Value = "Initial EN", Culture = English },
                    new PropertyValueModel { Alias = "variantValue", Value = "Initial DA", Culture = Danish },
                    new PropertyValueModel { Alias = "invariantValue", Value = "Initial shared" },
                ],
            },
            new HashSet<string> { English, Danish },
            Constants.Security.SuperUserKey);
        Assert.That(created.Success, Is.True);

        var contentKey = created.Result.Content!.Key;
        // An update replaces every property, so the unchanged shared value is sent back as is.
        var properties = new List<PropertyValueModel>
        {
            new() { Alias = "variantValue", Value = "Changed EN", Culture = English },
            new() { Alias = "variantValue", Value = "Initial DA", Culture = Danish },
            new() { Alias = "invariantValue", Value = changeSharedValue ? "Changed shared" : "Initial shared" },
        };

        var updated = await ContentEditingService.UpdateAsync(
            contentKey,
            new ContentUpdateModel
            {
                Variants =
                [
                    new VariantModel { Culture = English, Name = "English" },
                    new VariantModel { Culture = Danish, Name = "Danish" },
                ],
                Properties = properties,
            },
            Constants.Security.SuperUserKey);
        Assert.That(updated.Success, Is.True);

        return contentKey;
    }

    // A culture-variant page with an invariant block list, published in English and Danish. Its element type
    // varies by culture and has a shared text property too. Then the Danish block text and/or the block's
    // shared text change, so English is published next with those changes still pending.
    private async Task<Guid> CreateContentWithBlockListAsync(
        bool changeDanishBlockText = false,
        bool changeBlockSharedValue = false)
    {
        await LanguageService.CreateAsync(new LanguageBuilder().WithCultureInfo(Danish).Build(), Constants.Security.SuperUserKey);

        var elementType = new ContentTypeBuilder()
            .WithAlias("blockElement")
            .WithName("Block Element")
            .WithIsElement(true)
            .WithContentVariation(ContentVariation.Culture)
            .AddPropertyType()
            .WithAlias("variantText")
            .WithName("Variant text")
            .WithDataTypeId(Constants.DataTypes.Textbox)
            .WithPropertyEditorAlias(Constants.PropertyEditors.Aliases.TextBox)
            .WithValueStorageType(ValueStorageType.Nvarchar)
            .WithVariations(ContentVariation.Culture)
            .Done()
            .AddPropertyType()
            .WithAlias("sharedText")
            .WithName("Shared text")
            .WithDataTypeId(Constants.DataTypes.Textbox)
            .WithPropertyEditorAlias(Constants.PropertyEditors.Aliases.TextBox)
            .WithValueStorageType(ValueStorageType.Nvarchar)
            .WithVariations(ContentVariation.Nothing)
            .Done()
            .Build();
        await ContentTypeService.CreateAsync(elementType, Constants.Security.SuperUserKey);

        var blockListDataType = new DataType(
            GetRequiredService<PropertyEditorCollection>()[Constants.PropertyEditors.Aliases.BlockList],
            GetRequiredService<IConfigurationEditorJsonSerializer>())
        {
            ConfigurationData = new Dictionary<string, object>
            {
                {
                    "blocks",
                    new BlockListConfiguration.BlockConfiguration[] { new() { ContentElementTypeKey = elementType.Key } }
                },
            },
            Name = "Blocks",
            DatabaseType = ValueStorageType.Ntext,
            ParentId = Constants.System.Root,
            CreateDate = DateTime.UtcNow,
        };
        await GetRequiredService<IDataTypeService>().CreateAsync(blockListDataType, Constants.Security.SuperUserKey);

        var contentType = new ContentTypeBuilder()
            .WithAlias("blockPage")
            .WithName("Block Page")
            .WithContentVariation(ContentVariation.Culture)
            .AddPropertyType()
            .WithAlias("blocks")
            .WithName("Blocks")
            .WithDataTypeId(blockListDataType.Id)
            .WithVariations(ContentVariation.Nothing)
            .Done()
            .Build();
        contentType.AllowedAsRoot = true;
        await ContentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);

        var content = new ContentBuilder()
            .WithContentType(contentType)
            .WithCultureName(English, "English")
            .WithCultureName(Danish, "Danish")
            .Build();
        content.SetValue("blocks", BlockListJson(elementType, "Initial DA", "Initial shared"));
        ContentService.Save(content);
        Assert.That(ContentService.Publish(content, [English, Danish]).Success, Is.True);

        content.SetValue(
            "blocks",
            BlockListJson(
                elementType,
                changeDanishBlockText ? "Changed DA" : "Initial DA",
                changeBlockSharedValue ? "Changed block shared" : "Initial shared"));
        ContentService.Save(content);

        return content.Key;
    }

    private string BlockListJson(IContentType elementType, string danishText, string sharedText)
    {
        var blockKey = BlockKey;
        var blockListValue = new BlockListValue
        {
            Layout = new Dictionary<string, IEnumerable<IBlockLayoutItem>>
            {
                { Constants.PropertyEditors.Aliases.BlockList, [new BlockListLayoutItem { ContentKey = blockKey }] },
            },
            ContentData =
            [
                new BlockItemData
                {
                    Key = blockKey,
                    ContentTypeAlias = elementType.Alias,
                    ContentTypeKey = elementType.Key,
                    Values =
                    [
                        new BlockPropertyValue { Alias = "variantText", Culture = English, Value = "Initial EN" },
                        new BlockPropertyValue { Alias = "variantText", Culture = Danish, Value = danishText },
                        new BlockPropertyValue { Alias = "sharedText", Value = sharedText },
                    ],
                },
            ],
            SettingsData = [],
            Expose = [new BlockItemVariation(blockKey, English), new BlockItemVariation(blockKey, Danish)],
        };
        return GetRequiredService<IJsonSerializer>().Serialize(blockListValue);
    }

    // A culture-and-segment page with a property that varies by segment only, published in both cultures
    // with a value for segment "s1" and then changed.
    private async Task<Guid> CreateContentWithPendingSegmentValueAsync()
    {
        await LanguageService.CreateAsync(new LanguageBuilder().WithCultureInfo(Danish).Build(), Constants.Security.SuperUserKey);

        var contentType = new ContentTypeBuilder()
            .WithAlias("segmentPage")
            .WithName("Segment Page")
            .WithContentVariation(ContentVariation.CultureAndSegment)
            .AddPropertyType()
            .WithAlias("segmentValue")
            .WithName("Segment value")
            .WithDataTypeId(Constants.DataTypes.Textbox)
            .WithVariations(ContentVariation.Segment)
            .Done()
            .Build();
        contentType.AllowedAsRoot = true;
        await ContentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);

        var content = new ContentBuilder()
            .WithContentType(contentType)
            .WithCultureName(English, "English")
            .WithCultureName(Danish, "Danish")
            .Build();
        content.SetValue("segmentValue", "Initial segment", culture: null, segment: "s1");
        ContentService.Save(content);
        Assert.That(ContentService.Publish(content, [English, Danish]).Success, Is.True);

        content.SetValue("segmentValue", "Changed segment", culture: null, segment: "s1");
        ContentService.Save(content);

        return content.Key;
    }

    private async Task<IUser> CreateServiceAccountAsync(bool hasAccessToInvariantForVariant)
    {
        var suffix = hasAccessToInvariantForVariant ? "WithShared" : "WithoutShared";
        var userGroup = UserGroupBuilder.CreateUserGroup(suffix: suffix);
        userGroup.HasAccessToAllLanguages = true;
        userGroup.HasAccessToInvariantForVariant = hasAccessToInvariantForVariant;
        var groupResult = await UserGroupService.CreateAsync(userGroup, Constants.Security.SuperUserKey);
        Assert.That(groupResult.Success, Is.True);

        var user = UserService.CreateUserWithIdentity($"service-{suffix}", $"service-{suffix}@test.com".ToLowerInvariant());
        user.AddGroup(userGroup.ToReadOnlyGroup());
        UserService.Save(user);
        return user;
    }
}
