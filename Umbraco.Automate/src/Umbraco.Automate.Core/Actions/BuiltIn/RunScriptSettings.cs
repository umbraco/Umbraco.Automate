using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="RunScriptAction"/>.
/// </summary>
public sealed class RunScriptSettings
{
    /// <summary>
    /// Gets or sets the JavaScript module source. The module must export a default function that
    /// receives the step inputs and returns a result.
    /// </summary>
    [Field(
        Label = "Script",
        Description = """
            JavaScript module. Export a default function that receives the step inputs as its single `data` argument and returns a result. The returned value becomes this step's output (as JSON) for later steps to bind to.

            ```js
            export default function (data) {
                return { upper: data.name.toUpperCase() };
            }
            ```
            """,
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = "["
            + "{ \"alias\": \"language\", \"value\": \"javascript\" },"
            + "{ \"alias\": \"height\", \"value\": 300 },"
            + "{ \"alias\": \"lineNumbers\", \"value\": true },"
            + "{ \"alias\": \"minimap\", \"value\": false },"
            + "{ \"alias\": \"wordWrap\", \"value\": true }"
            + "]")]
    public string Script { get; set; } =
        """
        export default function (data) {
            return data;
        }
        """;

    /// <summary>
    /// Gets or sets a value indicating whether the script may make outbound HTTP requests via
    /// <c>fetch</c>. Defaults to <c>true</c>.
    /// </summary>
    [Field(
        Label = "Allow fetch",
        Description = "Allow the script to make outbound HTTP requests using fetch(). Requests are SSRF-protected.",
        SortOrder = 1)]
    public bool AllowFetch { get; set; } = true;
}
