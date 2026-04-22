using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Deploy.Infrastructure.Disk;

namespace Umbraco.Automate.Deploy.Composing;

/// <summary>
/// Component for registering Umbraco Automate Deploy UDI types and disk entity types.
/// </summary>
public class UmbracoAutomateDeployComponent(IDiskEntityService diskEntityService) : IAsyncComponent
{
    /// <inheritdoc />
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        RegisterUdiTypes();
        RegisterDiskEntityTypes();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TerminateAsync(bool isRestarting, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private static void RegisterUdiTypes()
    {
        UdiParser.RegisterUdiType(UmbracoAutomateDeployConstants.UdiEntityType.Connection, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(UmbracoAutomateDeployConstants.UdiEntityType.Workspace, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup, UdiType.GuidUdi);
        UdiParser.RegisterUdiType(UmbracoAutomateDeployConstants.UdiEntityType.Automation, UdiType.GuidUdi);
    }

    private void RegisterDiskEntityTypes()
    {
        diskEntityService.RegisterDiskEntityType(UmbracoAutomateDeployConstants.UdiEntityType.Connection);
        diskEntityService.RegisterDiskEntityType(UmbracoAutomateDeployConstants.UdiEntityType.Workspace);
        diskEntityService.RegisterDiskEntityType(UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup);
        diskEntityService.RegisterDiskEntityType(UmbracoAutomateDeployConstants.UdiEntityType.Automation);
    }
}
