import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbUniqueTreeStore } from "@umbraco-cms/backoffice/tree";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import { UA_AUTOMATION_TREE_STORE_ALIAS } from "./constants.js";

export class UaAutomationTreeStore extends UmbUniqueTreeStore {
    constructor(host: UmbControllerHost) {
        super(host, UA_AUTOMATION_TREE_STORE_ALIAS);
    }
}

export default UaAutomationTreeStore;

export const UA_AUTOMATION_TREE_STORE_CONTEXT = new UmbContextToken<UaAutomationTreeStore>(
    UA_AUTOMATION_TREE_STORE_ALIAS,
);
