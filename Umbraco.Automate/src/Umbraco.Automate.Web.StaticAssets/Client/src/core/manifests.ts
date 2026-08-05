import { manifests as versionHistoryManifests } from "./version-history/manifests.js";
import { conditionBuilderManifests } from "./components/condition-builder/manifests.js";
import { switchCaseBuilderManifests } from "./components/switch-case-builder/manifests.js";
import { bindingPickerManifests } from "./components/binding-picker/manifests.js";
import { bindingTextBoxManifests } from "./components/binding-text-box/manifests.js";
import { bindingTextAreaManifests } from "./components/binding-text-area/manifests.js";
import { webhookAuthenticatorPickerManifests } from "./components/webhook-authenticator-picker/manifests.js";
import { webhookMethodPickerManifests } from "./components/webhook-method-picker/manifests.js";
import { webhookSecretFieldManifests } from "./components/webhook-secret-field/manifests.js";
import { sensitiveFieldManifests } from "./components/sensitive-field/manifests.js";
import { editorNotificationSeverityPickerManifests } from "./components/editor-notification-severity-picker/manifests.js";
import { memberTypePickerManifests } from "./components/member-type-picker/manifests.js";
import { userGroupPickerManifests } from "./components/user-group-picker/manifests.js";

export const manifests: UmbExtensionManifest[] = [
    ...versionHistoryManifests,
    ...conditionBuilderManifests,
    ...switchCaseBuilderManifests,
    ...bindingPickerManifests,
    ...bindingTextBoxManifests,
    ...bindingTextAreaManifests,
    ...webhookAuthenticatorPickerManifests,
    ...webhookMethodPickerManifests,
    ...webhookSecretFieldManifests,
    ...sensitiveFieldManifests,
    ...editorNotificationSeverityPickerManifests,
    ...memberTypePickerManifests,
    ...userGroupPickerManifests,
];
