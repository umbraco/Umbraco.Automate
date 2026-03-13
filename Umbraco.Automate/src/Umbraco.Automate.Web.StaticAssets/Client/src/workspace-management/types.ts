import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";

export interface UaWorkspaceDetailModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    serviceAccountKey: string;
    userGroups: string[];
    allowedConnections: string[];
    version: number;
    dateCreated: string;
    dateModified: string;
}

export interface UaWorkspaceItemModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    serviceAccountKey: string;
    version: number;
    dateCreated: string;
    dateModified: string;
}
