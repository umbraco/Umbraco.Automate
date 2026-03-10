using System.Text.Json;
using DotNetCore.CAP;
using DotNetCore.CAP.Messages;
using DotNetCore.CAP.Transport;
using Umbraco.Cms.Persistence.EFCore.Scoping;

namespace Umbraco.Automate.Persistence.Transport;

/// <summary>
/// Database-backed <see cref="ITransport"/> that writes CAP messages to a shared table.
/// All Umbraco nodes share the same database, so messages are visible to all consumers.
/// </summary>
internal sealed class DatabaseTransport : ITransport
{
    private readonly IEFCoreScopeProvider<UmbracoAutomateDbContext> _scopeProvider;

    public DatabaseTransport(IEFCoreScopeProvider<UmbracoAutomateDbContext> scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public BrokerAddress BrokerAddress { get; } = new("Database", "localhost");

    public async Task<OperateResult> SendAsync(TransportMessage message)
    {
        using var scope = _scopeProvider.CreateScope();

        await scope.ExecuteWithContextAsync<Task>(async db =>
        {
            db.TransportMessages.Add(new TransportMessageEntity
            {
                Topic = message.GetName() ?? string.Empty,
                Headers = JsonSerializer.Serialize(message.Headers),
                Body = System.Text.Encoding.UTF8.GetString(message.Body.Span),
                CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        scope.Complete();
        return OperateResult.Success;
    }
}
