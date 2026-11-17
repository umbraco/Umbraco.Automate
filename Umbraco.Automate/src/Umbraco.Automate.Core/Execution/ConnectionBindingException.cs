namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Thrown when a step's <c>ConnectionBinding</c> cannot be turned into a connection the step may
/// use: the binding resolved to no value or to something that is not a connection ID, or the
/// connection does not exist, is not allowed in the automation's workspace, or is of a different
/// type than the action requires. Derives from <see cref="InvalidOperationException"/> so the
/// step error classifier treats it as a terminal configuration error: retrying cannot help.
/// </summary>
internal sealed class ConnectionBindingException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionBindingException"/> class.
    /// </summary>
    /// <param name="message">The message describing why the binding could not be used.</param>
    public ConnectionBindingException(string message)
        : base(message)
    {
    }
}
