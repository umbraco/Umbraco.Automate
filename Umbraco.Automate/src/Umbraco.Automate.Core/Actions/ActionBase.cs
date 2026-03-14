using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Base class for actions that reads metadata from the <see cref="ActionAttribute"/>
/// and auto-derives settings schema.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ActionBase<TSettings> : StepTypeBase<TSettings>, IAction
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings}"/> class.
    /// </summary>
    protected ActionBase(ActionInfrastructure infrastructure) : base(infrastructure) { }

    /// <inheritdoc />
    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
