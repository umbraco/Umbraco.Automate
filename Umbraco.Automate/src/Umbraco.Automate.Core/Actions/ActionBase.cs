using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Base class for actions that produce typed output. Reads metadata from the <see cref="ActionAttribute"/>
/// and auto-derives settings and output schemas. Provides typed convenience methods for returning output data.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
/// <typeparam name="TOutput">The output POCO type describing the data this action produces.</typeparam>
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

    /// <summary>
    /// Creates a successful result with typed output data.
    /// </summary>
    /// <param name="output">The output data matching the declared <typeparamref name="TOutput"/> schema.</param>
    protected static ActionResult Success(TOutput output) => ActionResult.Success(output);

    /// <summary>
    /// Creates a successful result with a named outcome and typed output data.
    /// </summary>
    /// <param name="outcome">The named outcome for branching.</param>
    /// <param name="output">The output data matching the declared <typeparamref name="TOutput"/> schema.</param>
    protected static ActionResult SuccessWithOutcome(string outcome, TOutput output) => ActionResult.SuccessWithOutcome(outcome, output);

    /// <summary>
    /// Creates a result that durably sleeps for the specified duration with typed output data.
    /// </summary>
    /// <param name="duration">The duration to sleep for.</param>
    /// <param name="output">The output data matching the declared <typeparamref name="TOutput"/> schema.</param>
    protected static ActionResult Sleep(TimeSpan duration, TOutput output) => ActionResult.Sleep(duration, output);
}

/// <summary>
/// Base class for actions that produce no output. Reads metadata from the <see cref="ActionAttribute"/>
/// and auto-derives settings schema. Use <see cref="ActionBase{TSettings, TOutput}"/> instead if
/// the action produces output data.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class ActionBase<TSettings> : StepTypeBase<TSettings, object, ActionAttribute, ActionInfrastructure>, IAction
    where TSettings : class, new()
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionBase{TSettings}"/> class.
    /// </summary>
    protected ActionBase(ActionInfrastructure infrastructure) : base(infrastructure) { }

    /// <inheritdoc />
    public abstract Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a successful result with no output data.
    /// </summary>
    protected static ActionResult Success() => ActionResult.Success();

    /// <summary>
    /// Creates a successful result with a named outcome and no output data.
    /// </summary>
    /// <param name="outcome">The named outcome for branching.</param>
    protected static ActionResult SuccessWithOutcome(string outcome) => ActionResult.SuccessWithOutcome(outcome);

    /// <summary>
    /// Creates a result that durably sleeps for the specified duration with no output data.
    /// </summary>
    /// <param name="duration">The duration to sleep for.</param>
    protected static ActionResult Sleep(TimeSpan duration) => ActionResult.Sleep(duration);
}
