import type { UmbEntityModel } from "@umbraco-cms/backoffice/entity";

export interface UaConnectionDetailModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    type: string;
    settings: Record<string, unknown>;
    version: number;
    dateCreated: string;
    dateModified: string;
}

export interface UaConnectionItemModel extends UmbEntityModel {
    unique: string;
    entityType: string;
    alias: string;
    name: string;
    type: string;
    version: number;
    dateCreated: string;
    dateModified: string;
}
