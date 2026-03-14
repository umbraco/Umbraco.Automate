using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Base class for actions that reads metadata from the <see cref="ActionAttribute"/>
/// and auto-derives settings and output schemas.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TOutput">The output POCO type (use <see cref="object"/> if no output).</typeparam>
public abstract class ActionBase<TSettings, TOutput> : StepTypeBase<TSettings, TOutput, ActionAttribute, ActionInfrastructure>, IAction
    where TSettings : class, new()
    where TOutput : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings, TOutput}"/> class.
    /// </summary>
    protected ActionBase(ActionInfrastructure infrastructure) : base(infrastructure) { }

    /// <inheritdoc />
    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Base class for actions with no output.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ActionBase<TSettings> : ActionBase<TSettings, object>
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings}"/> class.
    /// </summary>
    protected ActionBase(ActionInfrastructure infrastructure) : base(infrastructure) { }
}
