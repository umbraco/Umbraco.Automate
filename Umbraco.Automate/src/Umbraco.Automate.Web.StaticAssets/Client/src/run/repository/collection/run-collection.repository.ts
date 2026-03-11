import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UaRunCollectionServerDataSource, type UaRunCollectionFilter } from "./run-collection.server.data-source.js";

export class UaRunCollectionRepository extends UmbRepositoryBase {
    #collectionSource: UaRunCollectionServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#collectionSource = new UaRunCollectionServerDataSource(host);
    }

    async requestCollection(filter: UaRunCollectionFilter) {
        return this.#collectionSource.getCollection(filter);
    }
}

export { UaRunCollectionRepository as api };
