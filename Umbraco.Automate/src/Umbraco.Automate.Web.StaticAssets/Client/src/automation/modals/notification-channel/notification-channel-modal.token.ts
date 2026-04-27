import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { UaNotificationChannelModalData, UaNotificationChannelModalValue } from "./types.js";

export const UA_NOTIFICATION_CHANNEL_MODAL = new UmbModalToken<
    UaNotificationChannelModalData,
    UaNotificationChannelModalValue
>("UmbracoAutomate.Modal.NotificationChannel", {
    modal: {
        type: "sidebar",
        size: "small",
    },
});
