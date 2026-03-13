import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbUniqueTreeStore } from "@umbraco-cms/backoffice/tree";
import { UmbContextToken } from "@umbraco-cms/backoffice/context-api";
import { UA_CONNECTION_TREE_STORE_ALIAS } from "./constants.js";

export class UaConnectionTreeStore extends UmbUniqueTreeStore {
    constructor(host: UmbControllerHost) {
        super(host, UA_CONNECTION_TREE_STORE_ALIAS);
    }
}

export default UaConnectionTreeStore;

export const UA_CONNECTION_TREE_STORE_CONTEXT = new UmbContextToken<UaConnectionTreeStore>(
    UA_CONNECTION_TREE_STORE_ALIAS,
);
