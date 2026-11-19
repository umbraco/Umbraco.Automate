# Spec: Survive the installer restart without a connection string

## Endpoints
None.

## Rules
- **R1** A site that boots with no Automate connection string starts without an exception, and logs the warning once for Automate and once for OpenIddict. Core's text: "Umbraco Automate has no database connection string configured yet. Automate stays inactive until one is configured. A connection string added by the installer is picked up straight away; one added any other way needs the application to restart." OpenIddict's has "OAuth connections" in place of "Automate".
- **R2** While no connection string resolves, every context from `IDbContextFactory<T>` and `IDetachedDbContextFactory<T>` uses the placeholder, and creating one doesn't throw.
- **R3** Once a connection string resolves, for example after the backoffice installer writes one, the next context created uses the real database. No process restart is needed. This applies to both `UmbracoAutomateDbContext` and `OpenIddictDbContext`.
- **R4** The switch logs one information line per context: "Umbraco Automate found its database connection string and is now using it." (OpenIddict: "…OAuth connections are now available.")
- **R5** The real pool is built once per context, even when many creates race at the moment the connection string appears.
- **R6** A site that has its connection string at boot uses DI's pooled factory exactly as today. No second pool is built.
- **R7** Contexts on the real pool have the same provider setup, migrations assembly, history table and interceptors as DI's pool. Writes still wait for Automate's migrations (`AutomateReadinessInterceptor`).
- **R8** When an ambient CMS transaction exists and Automate shares the CMS database, the enlisted context is used as today, ahead of any pool.
- **R9 (end to end)** On a fresh backoffice install with no connection strings, the site stays up after Install. In the same process, login works, and a Manual automation with a Log Message step can be created, published and run to Completed. OAuth credential cleanup runs without errors. This is the brief's Repro.

## Targets
- No change to the create cost on a site that has its connection string at boot, beyond one flag read.
- While on the placeholder, a create costs one connection-string lookup (`DatabaseConnectionInfo.Resolve`, a config read with no I/O), with no throttle. Once the real connection is in use, it costs one flag check.
