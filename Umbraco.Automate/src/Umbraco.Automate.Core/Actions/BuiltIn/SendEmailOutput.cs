namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="SendEmailAction"/>.
/// </summary>
public sealed class SendEmailOutput
{
    /// <summary>
    /// Gets the number of recipients the email was sent to.
    /// </summary>
    public int RecipientCount { get; init; }

    /// <summary>
    /// Gets the subject line that was sent.
    /// </summary>
    public string Subject { get; init; } = string.Empty;
}
