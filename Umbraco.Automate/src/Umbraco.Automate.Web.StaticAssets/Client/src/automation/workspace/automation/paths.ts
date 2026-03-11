import { UMB_WORKSPACE_PATH_PATTERN } from "@umbraco-cms/backoffice/workspace";
import { UmbPathPattern } from "@umbraco-cms/backoffice/router";
import { UA_SECTION_PATHNAME } from "../../../section/constants.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../constants.js";

export const UA_AUTOMATION_WORKSPACE_PATH = UMB_WORKSPACE_PATH_PATTERN.generateAbsolute({
    sectionName: UA_SECTION_PATHNAME,
    entityType: UA_AUTOMATION_ENTITY_TYPE,
});

export const UA_CREATE_AUTOMATION_WORKSPACE_PATH_PATTERN = new UmbPathPattern<Record<string, never>>(
    "create",
    UA_AUTOMATION_WORKSPACE_PATH,
);

export const UA_EDIT_AUTOMATION_WORKSPACE_PATH_PATTERN = new UmbPathPattern<{ unique: string }>(
    "edit/:unique",
    UA_AUTOMATION_WORKSPACE_PATH,
);
