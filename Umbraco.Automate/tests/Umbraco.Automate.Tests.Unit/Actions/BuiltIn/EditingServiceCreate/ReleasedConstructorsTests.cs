// Story S6: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Both actions shipped in 2026.10.1 with the constructors below. They stay, marked obsolete, and
/// DI picks the new constructor that takes the editing service.
/// </summary>
public class ReleasedConstructorsTests
{
    private static readonly Type[] _releasedCreateContentParameters =
    [
        typeof(ActionInfrastructure),
        typeof(IContentService),
        typeof(IContentTypeService),
        typeof(IUserIdKeyResolver),
        typeof(IBackOfficeSecurityAccessor),
        typeof(IUmbracoContextFactory),
        typeof(IAutomationActionAuthorizer),
        typeof(ILogger<CreateContentAction>),
    ];

    private static readonly Type[] _releasedCreateMediaParameters =
    [
        typeof(ActionInfrastructure),
        typeof(IMediaService),
        typeof(IMediaTypeService),
        typeof(IUserIdKeyResolver),
        typeof(IBackOfficeSecurityAccessor),
        typeof(IUmbracoContextFactory),
        typeof(IAutomationActionAuthorizer),
        typeof(IMediaFileDownloader),
        typeof(ILogger<CreateMediaAction>),
    ];

    [Fact(Skip = "Pending")]
    public void TheReleasedCreateContentConstructorIsObsolete()
        => typeof(CreateContentAction).GetConstructor(_releasedCreateContentParameters)
            .ShouldNotBeNull().GetCustomAttribute<ObsoleteAttribute>().ShouldNotBeNull();

    [Fact(Skip = "Pending")]
    public void TheReleasedCreateMediaConstructorIsObsolete()
        => typeof(CreateMediaAction).GetConstructor(_releasedCreateMediaParameters)
            .ShouldNotBeNull().GetCustomAttribute<ObsoleteAttribute>().ShouldNotBeNull();

    [Fact(Skip = "Pending")]
    public void DiPicksCreateContentsNewConstructor()
        => ConstructorTaking<IContentEditingService>(typeof(CreateContentAction))
            .ShouldNotBeNull().GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>().ShouldNotBeNull();

    [Fact(Skip = "Pending")]
    public void DiPicksCreateMediasNewConstructor()
        => ConstructorTaking<IMediaEditingService>(typeof(CreateMediaAction))
            .ShouldNotBeNull().GetCustomAttribute<ActivatorUtilitiesConstructorAttribute>().ShouldNotBeNull();

    private static ConstructorInfo? ConstructorTaking<TService>(Type actionType)
        => actionType.GetConstructors()
            .SingleOrDefault(c => c.GetParameters().Any(p => p.ParameterType == typeof(TService)));
}
