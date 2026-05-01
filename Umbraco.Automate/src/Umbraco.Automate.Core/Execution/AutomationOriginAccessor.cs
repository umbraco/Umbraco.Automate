namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IAutomationOriginAccessor"/>.
/// Origin set by action middleware flows through awaits — including nested awaits across
/// CMS service calls — into notification handlers running on the same async context.
/// </summary>
internal sealed class AutomationOriginAccessor : IAutomationOriginAccessor
{
    private static readonly AsyncLocal<AutomationOrigin?> CurrentValue = new();

    public AutomationOrigin? Current => CurrentValue.Value;

    public IDisposable Push(AutomationOrigin origin)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = origin;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly AutomationOrigin? _previous;
        private bool _disposed;

        public Scope(AutomationOrigin? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentValue.Value = _previous;
            _disposed = true;
        }
    }
}
