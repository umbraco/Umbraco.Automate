using Umbraco.Automate.Core.Configuration;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Maps the WorkflowCore engine settings in <see cref="ExecutionOptions"/> onto
/// <see cref="WorkflowOptions"/>.
/// </summary>
internal static class WorkflowEngineSettings
{
    /// <summary>
    /// Applies the engine settings. A value of zero or less leaves WorkflowCore's own default
    /// in place.
    /// </summary>
    internal static void Apply(WorkflowOptions cfg, ExecutionOptions options)
    {
        if (options.PollInterval > TimeSpan.Zero)
        {
            cfg.UsePollInterval(options.PollInterval);
        }

        if (options.MaxConcurrentRuns > 0)
        {
            cfg.UseMaxConcurrentWorkflows(options.MaxConcurrentRuns);
        }
    }
}
