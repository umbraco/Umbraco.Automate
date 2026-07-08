using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Regression guard for the WorkflowCore DI registration-order bug: a bare <c>AddWorkflow()</c>
/// call (or a raw <c>AddSingleton&lt;IQueueProvider&gt;</c>/<c>AddSingleton&lt;IDistributedLockProvider&gt;</c>
/// registered before <c>AddWorkflow()</c>) is silently shadowed because <c>AddWorkflow()</c>
/// unconditionally re-registers its own defaults afterward. This test replicates the exact
/// registration block from <c>UmbracoBuilderExtensions.Collections.cs</c>'s
/// <c>AddUmbracoAutomateCore</c> — using <c>UseQueueProvider</c>/<c>UseDistributedLockManager</c> —
/// and asserts the resolved implementations are ours, not WorkflowCore's
/// <c>SingleNodeQueueProvider</c>/<c>SingleNodeLockProvider</c>.
/// </summary>
public class WorkflowCoreDiCompositionTests
{
    [Fact]
    public void AddWorkflow_WithUseQueueProviderAndUseDistributedLockManager_ResolvesAppImplementations()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // OutboxQueueProvider's constructor deps.
        services.AddSingleton(Mock.Of<IOutbox>());

        // WorkflowLockProvider's constructor deps.
        services.AddSingleton(Mock.Of<IWorkflowLockStore>());
        services.Configure<WorkflowLockOptions>(_ => { });
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<OutboxQueueProvider>();
        services.AddSingleton<WorkflowLockProvider>();

        // Copied verbatim from UmbracoBuilderExtensions.Collections.cs's AddUmbracoAutomateCore.
        services.AddWorkflow(cfg =>
        {
            cfg.UseQueueProvider(sp => sp.GetRequiredService<OutboxQueueProvider>());
            cfg.UseDistributedLockManager(sp => sp.GetRequiredService<WorkflowLockProvider>());
        });

        var serviceProvider = services.BuildServiceProvider();

        var queueProvider = serviceProvider.GetRequiredService<IQueueProvider>();
        var lockProvider = serviceProvider.GetRequiredService<IDistributedLockProvider>();

        queueProvider.ShouldBeOfType<OutboxQueueProvider>();
        lockProvider.ShouldBeOfType<WorkflowLockProvider>();
    }
}
