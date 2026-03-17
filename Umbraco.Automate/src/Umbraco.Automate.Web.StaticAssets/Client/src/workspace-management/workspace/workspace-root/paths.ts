import { UMB_WORKSPACE_PATH_PATTERN } from "@umbraco-cms/backoffice/workspace";
import { UA_SECTION_PATHNAME } from "../../../section/constants.js";
import { UA_WORKSPACE_ROOT_ENTITY_TYPE } from "../../constants.js";

export const UA_WORKSPACE_ROOT_WORKSPACE_PATH = UMB_WORKSPACE_PATH_PATTERN.generateAbsolute({
    sectionName: UA_SECTION_PATHNAME,
    entityType: UA_WORKSPACE_ROOT_ENTITY_TYPE,
});
