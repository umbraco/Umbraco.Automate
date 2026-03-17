import type { UmbCollectionFilterModel, UmbCollectionRepository } from "@umbraco-cms/backoffice/collection";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UaConnectionCollectionServerDataSource } from "./connection-collection.server.data-source.js";

export class UaConnectionCollectionRepository extends UmbRepositoryBase implements UmbCollectionRepository {
    #collectionSource: UaConnectionCollectionServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#collectionSource = new UaConnectionCollectionServerDataSource(host);
    }

    async requestCollection(filter: UmbCollectionFilterModel) {
        return this.#collectionSource.getCollection(filter);
    }
}

export { UaConnectionCollectionRepository as api };
