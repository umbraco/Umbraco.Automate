import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { ChannelConfigurationModel, NotificationChannelItemResponseModel } from "../../../api/types.gen.js";

export interface UaNotificationChannelSettingsModalData {
    channel: ChannelConfigurationModel;
    catalogueItem: NotificationChannelItemResponseModel;
}

export interface UaNotificationChannelSettingsModalValue {
    channel: ChannelConfigurationModel;
}

export const UA_NOTIFICATION_CHANNEL_SETTINGS_MODAL = new UmbModalToken<
    UaNotificationChannelSettingsModalData,
    UaNotificationChannelSettingsModalValue
>("UmbracoAutomate.Modal.NotificationChannelSettings", {
    modal: {
        type: "sidebar",
        size: "small",
    },
});
