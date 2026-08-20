using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="EFCoreWorkflowLockStore"/> against real SQLite, focused on the
/// concurrent-first-acquire race <see cref="EFCoreWorkflowLockStore.TryAcquireAsync"/> resolves with
/// a conditional insert instead of an insert-then-catch pattern (see PR #235 / issue #224).
/// </summary>
public class EFCoreWorkflowLockStoreTests : IDisposable
{
    private readonly EfCoreTestFixture _fixture = new();

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task TryAcquireAsync_ConcurrentFirstAcquire_ExactlyOneWinsAndNeitherThrows()
    {
        const string lockId = "race-lock";
        var winnerToken = Guid.NewGuid();
        var loserToken = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expiresUtc = now.AddSeconds(30);

        var winnerStore = new EFCoreWorkflowLockStore(new TestDbContextFactory(_fixture.CreateContext));

        // Deterministically reproduce the race window the conditional insert guards: right before the
        // "loser" call's INSERT executes, a different writer commits the same never-seen lock id first.
        // A real EFCoreWorkflowLockStore.TryAcquireAsync call is used for that other writer (rather than
        // hand-rolling the insert) so this test also exercises the ordinary, uncontended path.
        var interceptor = new InjectBeforeInsertInterceptor(() =>
            winnerStore.TryAcquireAsync(lockId, winnerToken, now, expiresUtc, CancellationToken.None)
                .GetAwaiter().GetResult());
        var racingFactory = new TestDbContextFactory(() => _fixture.CreateContext(b => b.AddInterceptors(interceptor)));
        var loserStore = new EFCoreWorkflowLockStore(racingFactory);

        // Neither call may throw to its own caller: an insert-then-catch implementation would also
        // satisfy that much, by swallowing the loser's DbUpdateException internally. What actually
        // distinguishes this fix is that the loser's database command itself never fails — no
        // exception reaches the ADO layer at all — which is the only thing that stops EF Core from
        // logging at Error severity. AnyCommandFailed asserts that directly.
        var loserAcquired = await loserStore.TryAcquireAsync(
            lockId, loserToken, now, expiresUtc, CancellationToken.None);

        loserAcquired.ShouldBeFalse();
        interceptor.Fired.ShouldBeTrue();
        interceptor.AnyCommandFailed.ShouldBeFalse();

        await using var verify = _fixture.CreateContext();
        var row = verify.WorkflowLocks.Single(l => l.LockId == lockId);
        row.OwnerToken.ShouldBe(winnerToken);
    }

    // Fires once, immediately before the first INSERT command reaches the database, so a "losing"
    // TryAcquireAsync call can be interleaved with a "winning" one at the exact point that would
    // otherwise trigger a primary key violation. Scoped to INSERT specifically so it doesn't also
    // fire for TryAcquireAsync's earlier steal-attempt UPDATE. Hooks both NonQueryExecutingAsync and
    // ReaderExecutingAsync: EF Core's SQLite provider runs a plain ExecuteSqlRawAsync as a non-query,
    // but routes a SaveChangesAsync-generated INSERT through the reader path instead (to read back
    // affected-row/RETURNING data), so a test exercising both an old insert-then-catch implementation
    // and this fix's raw-SQL one needs to catch the command on whichever path it takes. Also records
    // whether any command on this context ever failed, which is the actual signal EF Core's
    // Error-level command/save-failure logging is tied to — the store's own return value can't
    // distinguish "never failed" from "failed, but the exception was caught before reaching the caller."
    private sealed class InjectBeforeInsertInterceptor : DbCommandInterceptor
    {
        private readonly Action _onFirstInsert;
        private int _fired;
        private int _commandFailed;

        public InjectBeforeInsertInterceptor(Action onFirstInsert) => _onFirstInsert = onFirstInsert;

        public bool Fired => _fired != 0;

        public bool AnyCommandFailed => _commandFailed != 0;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            TryFireOnInsert(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            TryFireOnInsert(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void TryFireOnInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                _onFirstInsert();
            }
        }

        public override Task CommandFailedAsync(
            DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref _commandFailed, 1);
            return base.CommandFailedAsync(command, eventData, cancellationToken);
        }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        {
            Interlocked.Exchange(ref _commandFailed, 1);
            base.CommandFailed(command, eventData);
        }
    }
}
