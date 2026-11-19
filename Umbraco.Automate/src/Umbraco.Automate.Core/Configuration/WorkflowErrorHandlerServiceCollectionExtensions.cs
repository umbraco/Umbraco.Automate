using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Automate.Core.Execution;
using WorkflowCore.Interface;
using WorkflowCore.Services.ErrorHandlers;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Registration for Automate's WorkflowCore error handling.
/// </summary>
internal static class WorkflowErrorHandlerServiceCollectionExtensions
{
    /// <summary>
    /// Replaces WorkflowCore's stock <see cref="RetryHandler"/> with <see cref="AutomateRetryHandler"/>,
    /// so a step that retrying cannot fix ends the run rather than being retried. Must be called after
    /// <c>AddWorkflow</c>: WorkflowCore runs every registered handler whose type matches, so leaving the
    /// stock handler in place alongside this one would apply both.
    /// </summary>
    /// <exception cref="InvalidOperationException">WorkflowCore's handlers have not been registered yet.</exception>
    internal static IServiceCollection ReplaceWorkflowRetryHandler(this IServiceCollection services)
    {
        var stockRetryHandlers = services
            .Where(d => d.ServiceType == typeof(IWorkflowErrorHandler) && d.ImplementationType == typeof(RetryHandler))
            .ToList();

        if (stockRetryHandlers.Count == 0)
        {
            throw new InvalidOperationException(
                $"WorkflowCore's {nameof(RetryHandler)} is not registered. Call AddWorkflow before {nameof(ReplaceWorkflowRetryHandler)}.");
        }

        foreach (var descriptor in stockRetryHandlers)
        {
            services.Remove(descriptor);
        }

        // The stock handlers, resolvable by concrete type so AutomateRetryHandler can delegate to them.
        services.TryAddTransient<RetryHandler>();
        services.TryAddTransient<TerminateHandler>();
        services.AddTransient<IWorkflowErrorHandler, AutomateRetryHandler>();

        return services;
    }
}
