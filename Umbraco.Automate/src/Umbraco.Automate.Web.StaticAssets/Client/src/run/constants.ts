export { UA_RUN_ENTITY_TYPE } from "./entity.js";
export type { UaRunEntityType } from "./entity.js";

export * from "./workspace/constants.js";
export * from "./repository/constants.js";

export const UA_RUN_ICON = "icon-nodes";

/**
 * Server-event source emitted by the C# RunServerEventBridge. Subscribe via the
 * UMB_MANAGEMENT_API_SERVER_EVENT_CONTEXT to receive live run lifecycle updates.
 */
export const UA_RUN_EVENT_SOURCE = "Umbraco:Automate:Run";

/** Event types corresponding to RunServerEventBridge's RunStartedEventType / RunUpdatedEventType. */
export const UA_RUN_EVENT_TYPES = ["Started", "Updated"] as const;
