import type { UmbCollectionFilterModel, UmbCollectionRepository } from "@umbraco-cms/backoffice/collection";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import { UaAutomationCollectionServerDataSource } from "./automation-collection.server.data-source.js";

export class UaAutomationCollectionRepository extends UmbRepositoryBase implements UmbCollectionRepository {
    #collectionSource: UaAutomationCollectionServerDataSource;

    constructor(host: UmbControllerHost) {
        super(host);
        this.#collectionSource = new UaAutomationCollectionServerDataSource(host);
    }

    async requestCollection(filter: UmbCollectionFilterModel) {
        return this.#collectionSource.getCollection(filter);
    }
}

export { UaAutomationCollectionRepository as api };
