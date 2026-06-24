import { UMB_SECTION_USER_PERMISSION_CONDITION_ALIAS } from "@umbraco-cms/backoffice/section";
import { UA_SECTION_ALIAS } from "../../section/constants.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../constants.js";
import {
    UA_AUTOMATION_SEARCH_PROVIDER_ALIAS,
    UA_AUTOMATION_GLOBAL_SEARCH_ALIAS,
    UA_AUTOMATION_SEARCH_RESULT_ITEM_ALIAS,
} from "./constants.js";

export const automationSearchManifests: Array<UmbExtensionManifest> = [
    {
        type: "searchProvider",
        alias: UA_AUTOMATION_SEARCH_PROVIDER_ALIAS,
        name: "Automation Search Provider",
        api: () => import("./automation.search-provider.js"),
        weight: 800,
        meta: {
            label: "Automations",
        },
    },
    {
        type: "searchResultItem",
        alias: UA_AUTOMATION_SEARCH_RESULT_ITEM_ALIAS,
        name: "Automation Search Result Item",
        element: () => import("./automation-search-result-item.element.js"),
        forEntityTypes: [UA_AUTOMATION_ENTITY_TYPE],
    },
    {
        type: "globalSearch",
        alias: UA_AUTOMATION_GLOBAL_SEARCH_ALIAS,
        name: "Automation Global Search",
        api: () => import("./automation-global-search.js"),
        weight: 800,
        meta: {
            label: "Automations",
            searchProviderAlias: UA_AUTOMATION_SEARCH_PROVIDER_ALIAS,
            sectionAlias: UA_SECTION_ALIAS,
        },
        conditions: [
            {
                alias: UMB_SECTION_USER_PERMISSION_CONDITION_ALIAS,
                match: UA_SECTION_ALIAS,
            },
        ],
    },
];
