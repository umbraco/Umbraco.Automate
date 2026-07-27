# Run Script action

The **Run Script** action (`umbracoAutomate.runScript`, Core group) runs a small, user-authored
JavaScript function to transform or compute data between steps — so editors can make inline data
tweaks themselves instead of asking a developer to build a custom action. It executes in a
sandboxed [Jint](https://github.com/sebastienros/jint) engine.

## Authoring contract

Write an ES module that exports a **default function**. It receives the step's resolved inputs as
its single `data` argument and returns a value that becomes the step's output:

```javascript
export default function (data) {
    return { upper: data.name.toUpperCase() };
}
```

- **Input** — `data` is the step's input mappings (bindings to trigger output and prior step
  outputs), as a plain JSON object.
- **Output** — the returned value is serialized to JSON (via `JSON.stringify` semantics) and
  exposed as the step's `result` output, bindable downstream as `${ steps.<alias>.result... }`.
  Functions and `undefined` become `null`; `NaN`/`Infinity` become `null`; dates become ISO
  strings; a circular reference fails the step with a runtime error.
- The function may be `async` and may `await` promises (including `fetch`).

## Declaring the output shape

By default the binding UI knows only that the step produces a `result` — it cannot know what is
inside it, because the shape is whatever the script returns. Fill in the **Output schema** setting
with a JSON Schema describing the return value and the binding picker offers the individual
properties:

```json
{
    "type": "object",
    "properties": {
        "upper": { "type": "string" },
        "length": { "type": "integer" }
    }
}
```

Downstream steps can then bind `${ steps.<alias>.result.upper }` with autocomplete, instead of
binding the whole `result` and picking it apart by hand. This mirrors how an AI agent exposes its
structured output schema to Automate.

- The setting is **optional**. Left empty, `result` stays bindable as a single opaque value —
  exactly as it behaves without a schema.
- The schema describes what sits **under** `result` — it is not flattened onto the output root,
  because a script may also return an array or a primitive.
- It is **advisory**: nothing forces the script's return value to match it. A mismatch does not
  fail the step; it only means the binding suggestions were wrong. (An AI agent's schema *is*
  enforced, because the provider is given it as a response format — there is no equivalent for
  arbitrary JavaScript.)
- A schema that is not usable — malformed JSON, not an object, or not a valid JSON Schema — is
  rejected at save time, alongside the other validation checks below. At design time the binding
  UI quietly falls back to the opaque `result` rather than breaking.

## fetch

When enabled, scripts can make outbound HTTP requests with a browser-compatible `fetch`:

```javascript
export default async function (data) {
    const response = await fetch('https://api.example.com/things');
    const things = await response.json();
    return things.filter(t => t.active).map(t => t.id);
}
```

`fetch` is **SSRF-protected** (http/https only; loopback, private, link-local and cloud-metadata
addresses are blocked) and supports `method`, `body`, and headers as an object, an array of pairs,
or a `Headers` instance. It is gated by both the tenant-wide master switch
(`Scripting:FetchEnabled`) and the per-step **Allow fetch** toggle — both must be on.

## Validation

Scripts are validated when the automation is **saved**: a script that has a syntax error, does not
export a default function, or is configured with an output schema that is not a valid JSON Schema
is rejected with a clear message, rather than only failing at run time.

## Configuration

Bound to `Umbraco:Automate:Scripting`:

| Setting | Default | Purpose |
| --- | --- | --- |
| `Enabled` | `true` | Tenant-wide kill switch for the action. |
| `FetchEnabled` | `true` | Master switch for `fetch`. |
| `FetchAllowedHosts` | `[]` (any) | Optional host allowlist for `fetch`. |
| `MaxMemoryBytes` | `5242880` (5 MB) | Per-script memory cap. |
| `MaxRecursionDepth` | `64` | Recursion cap. |
| `MaxArraySize` | `1000` | Array-size cap. |
| `MaxStatements` | `10000` | Statement-count cap. |
| `StatementTimeout` | `00:00:03` | Per-statement engine timeout. |
| `TotalExecutionTimeout` | `00:00:15` | Total run cap (also capped by the step timeout). |
| `HttpRequestTimeout` | `00:00:05` | Per-`fetch` timeout. |
| `MaxResponseBodyBytes` | `10485760` (10 MB) | Max `fetch` response body a script may read. |

```json
"Umbraco": { "Automate": { "Scripting": {
  "FetchAllowedHosts": [ "api.example.com" ],
  "MaxStatements": 20000
} } }
```

## Sandboxing & limits

The engine enforces memory, recursion, array-size and statement limits, plus a per-statement
timeout. Because the per-statement timeout cannot interrupt a never-resolving promise, a separate
total-execution timeout terminates the script regardless. Limit and timeout breaches fail the step
with a `Timeout` error category; uncaught script errors fail with `Unknown`; compile errors with
`Validation`.

## Security note

A Run Script step runs arbitrary JavaScript under the workspace's service-account identity. Access
is governed by the existing Automate section policy and the workspace membership required to edit an
automation. Administrators can disable the action entirely (`Enabled: false`) or disable outbound
`fetch` (`FetchEnabled: false`) tenant-wide.

## Logging

`console.log` / `warn` / `error` / etc. are written to the application log, tagged with the
automation, run, and step IDs. (Surfacing them in the backoffice run-details view is planned once
the run-view logging feature lands.)
