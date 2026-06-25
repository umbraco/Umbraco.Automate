import { UmbGlobalSearchBase } from "@umbraco-cms/backoffice/search";

/**
 * Bridges the global header search to the automation search provider.
 * The base class resolves the provider from `meta.searchProviderAlias` and
 * delegates `search` to it, which is all automations require.
 */
export class UaAutomationGlobalSearch extends UmbGlobalSearchBase {}

export { UaAutomationGlobalSearch as api };
