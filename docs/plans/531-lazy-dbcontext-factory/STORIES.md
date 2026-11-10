# Stories: Survive the installer restart without a connection string

**5 stories, 22 criteria.** Story ids are used by `PLAN.md` and the spec files, so never renumber them.

**Core**

- [S1](#s1-automate-picks-up-its-connection-string-without-a-restart) Automate picks up its connection string without a restart, 10 criteria
- [S2](#s2-a-site-configured-at-boot-keeps-todays-path) A site configured at boot keeps today's path, 2 criteria
- [S3](#s3-the-startup-warning-says-what-to-do) The startup warning says what to do, 2 criteria

**OpenIddict**

- [S4](#s4-oauth-connections-pick-up-the-connection-string-too) OAuth connections pick up the connection string too, 4 criteria

**Shipping**

- [S5](#s5-a-fresh-install-works-end-to-end) A fresh install works end to end, 4 criteria (manual smoke, not a spec)

<details>
<summary>Ready and Done</summary>

The same agreement as the repo's earlier plans (`docs/plans/action-outcomes/STORIES.md`).

- **Ready:** role, capability and value stated. Given/When/Then covers the happy path. Out of scope stated. Passes INVEST, Small and Testable especially.
- **Done:** every criterion passes as an executable spec, sad paths included, except S5, which passes as the manual smoke check. `dotnet build` and the Umbraco.Automate and OpenIddict tests are green. The change is reviewed. The v17 port is tracked in `PLAN.md`.

</details>

---

## S1: Automate picks up its connection string without a restart

As a **developer setting up a new site through the backoffice installer**,
I want Automate to start using the database connection string once it exists,
so that the site keeps working after Install without restarting the application.

Covers SPEC R2, R3, R4, R5, R7. Out of scope: a site that never gets a connection string. It stays on the placeholder, as today.

**Happy path**

- **AC1: Placeholder before a connection string exists**
  Given Automate's DbContext is registered and no Automate connection string resolves
  When a context is created from `IDbContextFactory<UmbracoAutomateDbContext>`
  Then its connection string is `Data Source=:memory:`
- **AC2: Creating a context doesn't throw**
  Given no Automate connection string resolves
  When a context is created
  Then no exception is thrown
- **AC3: The real connection is used once it resolves**
  Given a context was created on the placeholder
  When the Automate connection string is added to configuration and another context is created
  Then that context's connection string is the configured one
- **AC4: The real database is reachable**
  Given the connection string was added after boot, pointing at a migrated SQLite database
  When a context is created and queries `umbracoAutomateAutomation`
  Then the query returns without an error
- **AC5: The detached factory switches too**
  Given the connection string was added after boot
  When a context is created from `IDetachedDbContextFactory<UmbracoAutomateDbContext>`
  Then its connection string is the configured one
- **AC6: The switch is logged**
  Given the connection string was added after boot
  When the first context on the real connection is created
  Then one information line is logged: "Umbraco Automate found its database connection string and is now using it."
- **AC7: Real contexts keep the readiness gate**
  Given the connection string was added after boot
  When a context is created
  Then its options include `AutomateReadinessInterceptor`
- **AC8: Real contexts keep Automate's migration settings**
  Given the connection string was added after boot
  When a context is created
  Then its migrations history table is `__UmbracoAutomate_MigrationsHistory`

**Sad path and edges**

- **AC9: Racing creates build one pool**
  Given the connection string was added after boot
  When 50 contexts are created in parallel
  Then the "found its database connection string" line is logged once
- **AC10: Still missing, still the placeholder**
  Given a context was created on the placeholder
  When another context is created and the connection string still doesn't resolve
  Then its connection string is `Data Source=:memory:`

## S2: A site configured at boot keeps today's path

As a **developer whose site already has Automate's connection string**,
I want Automate to create contexts exactly as it does today,
so that this fix adds no risk or cost to a working site.

Covers SPEC R6, R8.

**Happy path**

- **AC1: No second pool**
  Given the Automate connection string resolves at boot
  When several contexts are created
  Then the "found its database connection string" line is never logged
- **AC2: The ambient transaction still wins**
  Given Automate shares the CMS database and an ambient CMS transaction is open
  When a context is created
  Then it is enlisted on that transaction

## S3: The startup warning says what to do

As a **developer reading the log of a site with no connection string**,
I want the warning to say what happens next,
so that I don't restart the application when the installer is about to fix it.

Covers SPEC R1.

**Happy path**

- **AC1: New wording**
  Given no Automate connection string resolves
  When the pooled factory is first resolved
  Then one warning is logged: "Umbraco Automate has no database connection string configured yet. Automate stays inactive until one is configured. A connection string added by the installer is picked up straight away; one added any other way needs the application to restart."
- **AC2: Old wording gone**
  Given the same site
  When the warning is logged
  Then it doesn't mention `RuntimeLevel.Run`

## S4: OAuth connections pick up the connection string too

As a **developer using OAuth connections on a newly installed site**,
I want OpenIddict's storage to start using the database once it's configured,
so that OAuth connections work after Install without a restart.

Covers SPEC R1 to R3 for `OpenIddictDbContext`. It uses the same shared helper as core, which removes the second copy of the fallback code (#532).

**Happy path**

- **AC1: Placeholder before a connection string exists**
  Given OpenIddict's DbContext is registered and no Automate connection string resolves
  When a context is created from `IDbContextFactory<OpenIddictDbContext>`
  Then its connection string is `Data Source=:memory:`
- **AC2: The real connection is used once it resolves**
  Given a context was created on the placeholder
  When the Automate connection string is added to configuration and another context is created
  Then that context's connection string is the configured one
- **AC3: Warning wording**
  Given no Automate connection string resolves
  When the pooled factory is first resolved
  Then one warning is logged: "Umbraco Automate OpenIddict has no database connection string configured yet. OAuth connections stay inactive until one is configured. A connection string added by the installer is picked up straight away; one added any other way needs the application to restart."
- **AC4: The switch is logged**
  Given the connection string was added after boot
  When the first context on the real connection is created
  Then one information line is logged: "Umbraco Automate OpenIddict found its database connection string and OAuth connections are now available."

## S5: A fresh install works end to end

As a **developer setting up a new site through the backoffice installer**,
I want the site to keep running after Install,
so that I can log in and use Automate straight away.

Covers SPEC R9, the brief's Repro. Checked by hand on the demo site (`feature-build`'s smoke step). It isn't an automated spec, because it needs the real installer.

**Happy path**

- **AC1: The site stays up**
  Given a demo site with no connection strings, `UseNamedConnectionString: umbracoDbDSN` and unattended install off
  When the backoffice installer is completed with SQLite
  Then the site is still serving requests a minute later
- **AC2: Login works**
  Given the install just finished, in the same process
  When the admin logs in to the backoffice
  Then login succeeds
- **AC3: An automation runs**
  Given the admin is logged in, in the same process
  When a Manual automation with a Log Message step is created, published and run
  Then the run reaches Completed
- **AC4: OAuth credential cleanup runs**
  Given the install just finished, in the same process
  When five minutes have passed since boot
  Then the log has no "Failed to clean up OAuth credentials" error
