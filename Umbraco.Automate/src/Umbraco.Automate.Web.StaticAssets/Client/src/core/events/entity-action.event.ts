import { UmbEntityActionEvent } from "@umbraco-cms/backoffice/entity-action";

export class UaEntityActionEvent extends UmbEntityActionEvent {
    static readonly CREATED = "ua-entity-created";
    static readonly UPDATED = "ua-entity-updated";
    static readonly DELETED = "ua-entity-deleted";

    static created(unique: string, entityType: string) {
        return new UaEntityActionEvent(UaEntityActionEvent.CREATED, unique, entityType);
    }

    static updated(unique: string, entityType: string) {
        return new UaEntityActionEvent(UaEntityActionEvent.UPDATED, unique, entityType);
    }

    static deleted(unique: string, entityType: string) {
        return new UaEntityActionEvent(UaEntityActionEvent.DELETED, unique, entityType);
    }

    constructor(type: string, unique: string, entityType: string) {
        super(type, { unique, entityType });
    }
}
