# Changelog - Umbraco.Automate.OpenIddict

All notable changes to Umbraco.Automate.OpenIddict will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [18.1.2](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.OpenIddict@18.1.0...Umbraco.Automate.OpenIddict@18.1.2) (2026-08-10)

### fix

* **openiddict:** Enlist the credentials DbContext in the ambient Umbraco transaction when it shares a database ([4902fec](https://github.com/umbraco/Umbraco.Automate/commit/4902feccf5337ad5986762fe0119b8df7c0a8fb9)), closes [#197](https://github.com/umbraco/Umbraco.Automate/issues/197)

## [18.1.0](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.OpenIddict@18.0.0...Umbraco.Automate.OpenIddict@18.1.0) (2026-07-22)

### feat

* **oauth:** Warn before authenticate when provider credentials are missing ([e5da787](https://github.com/umbraco/Umbraco.Automate/commit/e5da787d3a8322a75fe466e6d8ced29a76adfff7)), closes [#107](https://github.com/umbraco/Umbraco.Automate/issues/107)

## [18.0.0](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.OpenIddict@18.0.0-beta...Umbraco.Automate.OpenIddict@18.0.0) (2026-07-08)

### Internal

* Promote to stable 18.0.0 alongside Umbraco.Automate. No functional changes since 18.0.0-beta.

## [18.0.0-beta](https://github.com/umbraco/Umbraco.Automate/compare/Umbraco.Automate.OpenIddict@17.0.0-beta...Umbraco.Automate.OpenIddict@18.0.0-beta) (2026-06-24)

### fix

* **core,openiddict:** Resolve connection string at runtime, not composition ([fbeb0f6](https://github.com/umbraco/Umbraco.Automate/commit/fbeb0f60eebe66d369805ee4c7b0176e36520353))
* **oauth:** Persist original-case provider name from OAuth properties ([3319917](https://github.com/umbraco/Umbraco.Automate/commit/3319917e144b9f050c62b374dd65fd29d6b93f35))

## [17.0.0-beta](https://github.com/umbraco/Umbraco.Automate/releases/tag/Umbraco.Automate.OpenIddict@17.0.0-beta) (2026-06-09)

### feat

* **oauth:** Add UserAccessToken and well-known auth properties ([7e77c94](https://github.com/umbraco/Umbraco.Automate/commit/7e77c942abf263dd020d568c1008249076ef69cb))
* **oauth:** Validate OAuth credentials in OAuthConnectionTypeBase ([e78a013](https://github.com/umbraco/Umbraco.Automate/commit/e78a01376aefb9eb8f5d66f8b1d809a242e8b7bf))
* **openiddict:** Add OAuth connection infrastructure via OpenIddict WebIntegration ([27c2d09](https://github.com/umbraco/Umbraco.Automate/commit/27c2d093eeca376543ec9616e50db0b9174a958e))
* **provider,openiddict:** Generate appsettings schema for Slack ([d00d7b1](https://github.com/umbraco/Umbraco.Automate/commit/d00d7b1448ddefbdb2e66380b6c897613aeaef2b))

### fix

* **openiddict:** Fix OAuth flow routes, token extraction, and persistence ([c290cb1](https://github.com/umbraco/Umbraco.Automate/commit/c290cb1b4d0d767e948e106ab7ecebbaf1ed5f37))
* **openiddict:** Skip expiry storage when no refresh token is available ([2bd325e](https://github.com/umbraco/Umbraco.Automate/commit/2bd325e17652254ea8b54777865c31a75f6fe3d4))
* **openiddict:** Warn when storing credentials with expiry but no refresh token ([280571e](https://github.com/umbraco/Umbraco.Automate/commit/280571ed47f4294b4f332f7c16656643eef5c400)), closes [#6](https://github.com/umbraco/Umbraco.Automate/issues/6)
