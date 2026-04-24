using System.Runtime.CompilerServices;
using System.Text.Json;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Deploy.Artifacts;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;

namespace Umbraco.Automate.Deploy.Connectors.ServiceConnectors;

/// <summary>
/// Service connector for Automate Connections, responsible for synchronizing
/// connection entities during deploy operations.
/// </summary>
[UdiDefinition(UmbracoAutomateDeployConstants.UdiEntityType.Connection, UdiType.GuidUdi)]
public class UmbracoAutomateConnectionServiceConnector(
    IConnectionService connectionService,
    ConnectionTypeCollection connectionTypeCollection,
    UmbracoAutomateDeploySettingsAccessor settingsAccessor)
    : UmbracoAutomateEntityServiceConnectorBase<AutomateConnectionArtifact, Connection>(settingsAccessor)
{
    /// <inheritdoc />
    protected override int[] ProcessPasses => [2];

    /// <inheritdoc />
    protected override string[] ValidOpenSelectors =>
    [
        Constants.DeploySelector.This,
        Constants.DeploySelector.ThisAndDescendants,
        Constants.DeploySelector.DescendantsOfThis,
    ];

    /// <inheritdoc />
    protected override string OpenUdiName => "All Umbraco Automate Connections";

    /// <inheritdoc />
    public override string UdiEntityType => UmbracoAutomateDeployConstants.UdiEntityType.Connection;

    /// <inheritdoc />
    public override async Task<Connection?> GetEntityAsync(Guid id, CancellationToken cancellationToken = default)
        => await connectionService.GetConnectionAsync(id, cancellationToken);

    /// <inheritdoc />
    public override async IAsyncEnumerable<Connection> GetEntitiesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connections = await connectionService.GetAllConnectionsAsync(cancellationToken);
        foreach (var connection in connections)
        {
            yield return connection;
        }
    }

    /// <inheritdoc />
    public override string GetEntityName(Connection entity) => entity.Name;

    /// <inheritdoc />
    public override Task<AutomateConnectionArtifact?> GetArtifactAsync(
        GuidUdi udi,
        Connection? entity,
        CancellationToken cancellationToken = default)
    {
        if (entity == null)
        {
            return Task.FromResult<AutomateConnectionArtifact?>(null);
        }

        var dependencies = new ArtifactDependencyCollection();

        var filteredSettings = FilterSettings(entity.Settings);
        var settings = filteredSettings.Count > 0
            ? JsonSerializer.SerializeToElement(filteredSettings)
            : (JsonElement?)null;

        var artifact = new AutomateConnectionArtifact(udi, dependencies)
        {
            Alias = entity.Alias,
            Name = entity.Name,
            Type = entity.Type,
            Settings = settings,
        };

        return Task.FromResult<AutomateConnectionArtifact?>(artifact);
    }

    /// <inheritdoc />
    public override async Task ProcessAsync(
        ArtifactDeployState<AutomateConnectionArtifact, Connection> state,
        IDeployContext context,
        int pass,
        CancellationToken cancellationToken = default)
    {
        state.NextPass = GetNextPass(pass);

        switch (pass)
        {
            case 2:
                await Pass2Async(state, cancellationToken);
                break;
        }
    }

    private async Task Pass2Async(
        ArtifactDeployState<AutomateConnectionArtifact, Connection> state,
        CancellationToken cancellationToken)
    {
        var artifact = state.Artifact;

        // The connection's Type alias maps to an IConnectionType registered via DI. If the
        // package that contributes that type isn't installed on the target (e.g. deploying
        // a Slack connection to a Cloud site without the Slack package), the resulting row
        // would be orphaned — unresolvable at runtime and unrenderable in the UI. Fail the
        // deploy with a message pointing at the missing package instead.
        if (connectionTypeCollection.GetByAlias(artifact.Type) is null)
        {
            throw new InvalidOperationException(
                $"Target site does not contain a connection type with alias '{artifact.Type}' (connection '{artifact.Name}'). " +
                "Ensure the package providing this connection type is installed on the target.");
        }

        Dictionary<string, object?> settings = [];
        if (artifact.Settings.HasValue)
        {
            settings = artifact.Settings.Value.Deserialize<Dictionary<string, object?>>() ?? [];
        }

        if (state.Entity != null)
        {
            // Update existing connection
            var connection = state.Entity;
            connection.Alias = artifact.Alias!;
            connection.Name = artifact.Name;
            connection.Type = artifact.Type;

            // Merge settings: only overwrite keys present in the artifact
            foreach (var kvp in settings)
            {
                connection.Settings[kvp.Key] = kvp.Value;
            }

            state.Entity = await connectionService.UpdateConnectionAsync(connection, cancellationToken: cancellationToken);
        }
        else
        {
            // Create new connection, preserving the artifact's UDI so cross-environment
            // references resolve and redeployment stays idempotent.
            var connection = new Connection
            {
                Id = artifact.Udi.Guid,
                Alias = artifact.Alias!,
                Name = artifact.Name,
                Type = artifact.Type,
                Settings = settings,
            };

            state.Entity = await connectionService.CreateConnectionAsync(connection, cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Filters connection settings based on deploy configuration.
    /// Removes encrypted values and explicitly ignored fields.
    /// </summary>
    private Dictionary<string, object?> FilterSettings(Dictionary<string, object?> settings)
    {
        var config = _settingsAccessor.Settings.Connections;
        var filtered = new Dictionary<string, object?>(settings.Count);

        foreach (var kvp in settings)
        {
            // Skip explicitly ignored fields
            if (config.IgnoreSettings.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip encrypted values if configured
            if (config.IgnoreEncrypted && kvp.Value is string strValue && strValue.StartsWith("ENC:", StringComparison.Ordinal))
            {
                continue;
            }

            filtered[kvp.Key] = kvp.Value;
        }

        return filtered;
    }
}
