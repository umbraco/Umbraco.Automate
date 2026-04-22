using Umbraco.Automate.Deploy.Tree;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Deploy.Infrastructure.Disk;
using Umbraco.Deploy.Infrastructure.Transfer;

namespace Umbraco.Automate.Deploy.Composing;

/// <summary>
/// Component for registering Umbraco Automate Deploy UDI types, disk entity types
/// and transfer entity types (which enable Queue / Tree Restore / Partial Restore options in the back-office).
/// </summary>
public class UmbracoAutomateDeployComponent(
    IDiskEntityService diskEntityService,
    ITransferEntityService transferEntityService) : IAsyncComponent
{
    /// <inheritdoc />
    public Task InitializeAsync(bool isRestarting, CancellationToken cancellationToken)
    {
        RegisterUdiTypes();
        RegisterDiskEntityTypes();
        RegisterTransferEntityTypes();

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

    private void RegisterTransferEntityTypes()
    {
        transferEntityService.RegisterTransferEntityType(
            UmbracoAutomateDeployConstants.UdiEntityType.Workspace,
            new DeployRegisteredEntityTypeDetailOptions
            {
                SupportsQueueForTransfer = true,
                SupportsQueueForTransferOfDescendents = true,
                SupportsRestore = true,
                PermittedToRestore = true,
                SupportsPartialRestore = true,
                SupportsImportExport = true,
            },
            null,
            new DeployTransferRegisteredEntityTypeDetail.RemoteTreeDetail(
                UmbracoAutomateTreeHelper.GetWorkspaceTree,
                "Automate workspaces",
                UmbracoAutomateDeployConstants.UdiEntityType.Workspace));

        transferEntityService.RegisterTransferEntityType(
            UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup,
            new DeployRegisteredEntityTypeDetailOptions
            {
                SupportsQueueForTransfer = true,
                SupportsQueueForTransferOfDescendents = true,
                SupportsRestore = true,
                PermittedToRestore = true,
                SupportsPartialRestore = true,
                SupportsImportExport = true,
            },
            null,
            new DeployTransferRegisteredEntityTypeDetail.RemoteTreeDetail(
                UmbracoAutomateTreeHelper.GetAutomationTree,
                "Automate folders",
                UmbracoAutomateDeployConstants.UdiEntityType.WorkspaceGroup));

        transferEntityService.RegisterTransferEntityType(
            UmbracoAutomateDeployConstants.UdiEntityType.Automation,
            new DeployRegisteredEntityTypeDetailOptions
            {
                SupportsQueueForTransfer = true,
                SupportsRestore = true,
                PermittedToRestore = true,
                SupportsPartialRestore = true,
                SupportsImportExport = true,
            },
            null,
            new DeployTransferRegisteredEntityTypeDetail.RemoteTreeDetail(
                UmbracoAutomateTreeHelper.GetAutomationTree,
                "Automations",
                UmbracoAutomateDeployConstants.UdiEntityType.Automation));

        transferEntityService.RegisterTransferEntityType(
            UmbracoAutomateDeployConstants.UdiEntityType.Connection,
            new DeployRegisteredEntityTypeDetailOptions
            {
                SupportsQueueForTransfer = true,
                SupportsRestore = true,
                PermittedToRestore = true,
                SupportsPartialRestore = true,
                SupportsImportExport = true,
            },
            null,
            new DeployTransferRegisteredEntityTypeDetail.RemoteTreeDetail(
                UmbracoAutomateTreeHelper.GetConnectionTree,
                "Automate connections",
                UmbracoAutomateDeployConstants.UdiEntityType.Connection));
    }
}
