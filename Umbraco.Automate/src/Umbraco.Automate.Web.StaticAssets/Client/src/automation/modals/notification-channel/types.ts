import type { ChannelConfigurationModel, NotificationChannelItemResponseModel } from "../../../api/types.gen.js";

export interface UaNotificationChannelModalData {
    channel: ChannelConfigurationModel;
    catalogueItem: NotificationChannelItemResponseModel;
}

export interface UaNotificationChannelModalValue {
    channel: ChannelConfigurationModel;
}
