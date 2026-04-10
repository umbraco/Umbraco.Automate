namespace Umbraco.Automate.Deploy.Configuration;

/// <summary>
/// Configuration settings for Umbraco.Automate Deploy.
/// </summary>
public class UmbracoAutomateDeploySettings
{
    /// <summary>
    /// Connection deployment settings (filtering sensitive data).
    /// </summary>
    public UmbracoAutomateDeployConnectionSettings Connections { get; set; } = new();
}

/// <summary>
/// Configuration settings for Automate Connection deployment.
/// Controls how sensitive settings are filtered during deployment.
/// </summary>
public class UmbracoAutomateDeployConnectionSettings
{
    /// <summary>
    /// If true, ignore fields with encrypted values (values starting with "ENC:").
    /// ALLOWS: $ configuration references (e.g., "$MyService:ApiKey").
    /// BLOCKS: Encrypted values (ENC:...).
    /// </summary>
    public bool IgnoreEncrypted { get; set; } = true;

    /// <summary>
    /// Specific settings field names to always ignore during deployment.
    /// BLOCKS: All values for these specific fields (most specific control).
    /// Use this for fine-grained control over individual fields.
    /// </summary>
    public string[] IgnoreSettings { get; set; } = [];
}
