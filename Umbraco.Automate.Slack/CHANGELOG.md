# Changelog - Umbraco.Automate.Slack

All notable changes to Umbraco.Automate.Slack will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [17.0.1](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.Slack@17.0.0...Umbraco.Automate.Slack@17.0.1) (2026-07-22)

### Internal

* Bump to align with Umbraco.Automate 17.1.0 and Umbraco.Automate.OpenIddict 17.1.0.

## [17.0.0](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.Slack@17.0.0-beta.1...Umbraco.Automate.Slack@17.0.0) (2026-07-08)

### Miscellaneous

* Promote to stable **17.0.0**. No functional changes since `17.0.0-beta.1`; released alongside Umbraco.Automate 17.0.0 to keep the dependency on Umbraco.Automate.Core and Umbraco.Automate.OpenIddict aligned.

## [17.0.0-beta.1](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.Slack@17.0.0-beta...Umbraco.Automate.Slack@17.0.0-beta.1) (2026-06-24)

### fix

* **slack:** Copy generated appsettings schema to consuming sites via buildTransitive ([9a95dba](https://github.com/umbraco/Umbraco.Automate/commit/9a95dbaa747720e0c0170d5e78b2764038702b4e))

## [17.0.0-beta](https://github.com/umbraco/Umbraco.Automate/releases/tag/Umbraco.Automate.Slack@17.0.0-beta) (2026-06-09)

### feat

* **slack:** Generate appsettings schema for Slack ([d00d7b1](https://github.com/umbraco/Umbraco.Automate/commit/d00d7b1448ddefbdb2e66380b6c897613aeaef2b))
* **slack:** Add Slack OAuth V2 event handlers for bot and user scopes ([c23e2c6](https://github.com/umbraco/Umbraco.Automate/commit/c23e2c68f9cb0785488b3617c454918026e7bbf9))
* **slack:** Validate Slack connection via auth.test ([5f50866](https://github.com/umbraco/Umbraco.Automate/commit/5f50866330722df11981be765fef6ac4a975b8f6))

### fix

* **slack:** Disable default Content items in Slack csproj ([988b8fe](https://github.com/umbraco/Umbraco.Automate/commit/988b8fe77df73fdea392c5dd775cf3429ea72ad1))
