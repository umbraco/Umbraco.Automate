using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Connections;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Resolves the connection a step runs with.
/// </summary>
/// <remarks>
/// Priority:
/// <list type="number">
///   <item>An explicitly selected <see cref="StepConfiguration.ConnectionId"/>.</item>
///   <item>A <see cref="StepConfiguration.ConnectionBinding"/>, evaluated against the run's binding data.</item>
///   <item>Auto-resolve: the first connection in the workspace matching the action's connection type.</item>
/// </list>
/// A static connection ID is checked against the workspace when the automation is published. A
/// bound one is only known at run time, so it is checked here instead: it must exist, be allowed
/// in the workspace, and match the action's connection type. Any failure throws
/// <see cref="ConnectionBindingException"/> rather than falling back to auto-resolve, so a binding
/// that goes wrong never silently runs the step against a different connection.
/// </remarks>
internal sealed class StepConnectionResolver
{
    private readonly IConnectionService _connectionService;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StepConnectionResolver"/> class.
    /// </summary>
    public StepConnectionResolver(IConnectionService connectionService, BindingEvaluator bindingEvaluator, ILogger logger)
    {
        _connectionService = connectionService;
        _bindingEvaluator = bindingEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the connection for the given step.
    /// </summary>
    /// <param name="stepConfig">The step being executed.</param>
    /// <param name="connectionTypeAlias">The connection type the action requires, if any.</param>
    /// <param name="executionContext">The run's execution context, which carries the workspace's allowed connections.</param>
    /// <param name="bindingData">The binding data for this step (trigger, prior steps, loop iteration).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection, or <c>null</c> when the step uses none.</returns>
    /// <exception cref="ConnectionBindingException">The step's connection binding could not be resolved to an allowed connection.</exception>
    public async Task<ConfiguredConnection?> ResolveConnectionAsync(
        StepConfiguration stepConfig,
        string? connectionTypeAlias,
        AutomationExecutionContext? executionContext,
        IReadOnlyDictionary<string, object?> bindingData,
        CancellationToken cancellationToken)
    {
        if (stepConfig.ConnectionId is { } connectionId)
        {
            return await _connectionService.GetConfiguredConnectionAsync(connectionId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(stepConfig.ConnectionBinding))
        {
            return await ResolveBoundConnectionAsync(
                stepConfig.ConnectionBinding, connectionTypeAlias, executionContext, bindingData, cancellationToken);
        }

        if (connectionTypeAlias is not null && executionContext is not null)
        {
            return await ResolveConnectionByTypeAsync(stepConfig, connectionTypeAlias, executionContext, cancellationToken);
        }

        return null;
    }

    private async Task<ConfiguredConnection> ResolveBoundConnectionAsync(
        string binding,
        string? connectionTypeAlias,
        AutomationExecutionContext? executionContext,
        IReadOnlyDictionary<string, object?> bindingData,
        CancellationToken cancellationToken)
    {
        var resolved = _bindingEvaluator.Evaluate(binding, bindingData).Trim();

        if (resolved.Length == 0)
        {
            throw new ConnectionBindingException(
                $"Connection is bound to '{binding}', which resolved to no value.");
        }

        if (!Guid.TryParse(resolved, out var connectionId))
        {
            throw new ConnectionBindingException(
                $"Connection is bound to '{binding}', which resolved to '{resolved}'. Expected a connection ID.");
        }

        // Check the workspace first, so a connection outside the workspace is reported the same
        // way whether or not it exists — the step does not learn about connections it cannot use.
        if (executionContext is null || !executionContext.AllowedConnections.Contains(connectionId))
        {
            throw new ConnectionBindingException(
                $"Connection '{connectionId}' (bound to '{binding}') is not allowed in this automation's workspace.");
        }

        var connection = await _connectionService.GetConfiguredConnectionAsync(connectionId, cancellationToken)
            ?? throw new ConnectionBindingException(
                $"Connection '{connectionId}' (bound to '{binding}') was not found.");

        if (connectionTypeAlias is not null
            && !string.Equals(connection.Type, connectionTypeAlias, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConnectionBindingException(
                $"Connection '{connection.Name}' (bound to '{binding}') is of type '{connection.Type}', but this action requires a connection of type '{connectionTypeAlias}'.");
        }

        return connection;
    }

    private async Task<ConfiguredConnection?> ResolveConnectionByTypeAsync(
        StepConfiguration stepConfig,
        string connectionTypeAlias,
        AutomationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var allConfigured = await _connectionService.GetConfiguredConnectionsByIdsAsync(
            executionContext.AllowedConnections, cancellationToken);

        var matches = allConfigured
            .Where(c => string.Equals(c.Type, connectionTypeAlias, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            _logger.LogWarning(
                "No connection of type '{ConnectionTypeAlias}' found in workspace '{WorkspaceId}' for step {StepId}",
                connectionTypeAlias, executionContext.WorkspaceId, stepConfig.Id);

            return null;
        }

        if (matches.Count > 1)
        {
            _logger.LogWarning(
                "Multiple connections of type '{ConnectionTypeAlias}' found in workspace '{WorkspaceId}' for step {StepId}. Using first match '{ConnectionId}'",
                connectionTypeAlias, executionContext.WorkspaceId, stepConfig.Id, matches[0].Id);
        }

        return matches[0];
    }
}
