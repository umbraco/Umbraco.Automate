using DotNetCore.CAP;
using Umbraco.Automate.Core.Dispatch.Transport;

// ReSharper disable once CheckNamespace — CAP convention for discovery
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="CapOptions"/> to register the in-process transport.
/// </summary>
internal static class InProcessCapOptionsExtensions
{
    /// <summary>
    /// Uses an in-process channel-based transport for CAP.
    /// Suitable for single-process deployments.
    /// </summary>
    public static CapOptions UseInProcessTransport(this CapOptions options)
    {
        options.RegisterExtension(new InProcessCapOptionsExtension());
        return options;
    }
}
