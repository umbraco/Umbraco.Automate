using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Covers how <see cref="ExecutionOptions"/> reaches the WorkflowCore engine. WorkflowCore keeps
/// these settings in internal fields on <see cref="WorkflowOptions"/>, so they are read back by
/// reflection.
/// </summary>
public class WorkflowEngineSettingsTests
{
    private static readonly TimeSpan WorkflowCoreDefaultPollInterval = TimeSpan.FromSeconds(10);
    private static readonly int WorkflowCoreDefaultMaxConcurrentWorkflows = Math.Max(Environment.ProcessorCount, 4);

    [Fact]
    public void Apply_DefaultOptions_KeepsWorkflowCoreDefaults()
    {
        var cfg = new WorkflowOptions(new ServiceCollection());

        WorkflowEngineSettings.Apply(cfg, new ExecutionOptions());

        GetField<TimeSpan>(cfg, "PollInterval").ShouldBe(WorkflowCoreDefaultPollInterval);
        GetField<int>(cfg, "MaxConcurrentWorkflows").ShouldBe(WorkflowCoreDefaultMaxConcurrentWorkflows);
    }

    [Fact]
    public void Apply_ConfiguredValues_AppliesThem()
    {
        var cfg = new WorkflowOptions(new ServiceCollection());
        var options = new ExecutionOptions
        {
            PollInterval = TimeSpan.FromSeconds(3),
            MaxConcurrentRuns = 17,
        };

        WorkflowEngineSettings.Apply(cfg, options);

        GetField<TimeSpan>(cfg, "PollInterval").ShouldBe(TimeSpan.FromSeconds(3));
        GetField<int>(cfg, "MaxConcurrentWorkflows").ShouldBe(17);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_ZeroOrNegativeValues_KeepsWorkflowCoreDefaults(int value)
    {
        var cfg = new WorkflowOptions(new ServiceCollection());
        var options = new ExecutionOptions
        {
            PollInterval = TimeSpan.FromSeconds(value),
            MaxConcurrentRuns = value,
        };

        WorkflowEngineSettings.Apply(cfg, options);

        GetField<TimeSpan>(cfg, "PollInterval").ShouldBe(WorkflowCoreDefaultPollInterval);
        GetField<int>(cfg, "MaxConcurrentWorkflows").ShouldBe(WorkflowCoreDefaultMaxConcurrentWorkflows);
    }

    [Fact]
    public void ExecutionOptions_PollIntervalDefault_MatchesWorkflowCore()
    {
        new ExecutionOptions().PollInterval.ShouldBe(GetField<TimeSpan>(new WorkflowOptions(new ServiceCollection()), "PollInterval"));
    }

    private static T GetField<T>(WorkflowOptions cfg, string name)
    {
        var field = typeof(WorkflowOptions).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull($"WorkflowCore no longer has WorkflowOptions.{name}; update this test and WorkflowEngineSettings.");
        return (T)field.GetValue(cfg)!;
    }
}
