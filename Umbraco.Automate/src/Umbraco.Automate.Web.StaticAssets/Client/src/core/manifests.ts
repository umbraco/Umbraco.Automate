import { manifests as versionHistoryManifests } from "./version-history/manifests.js";
import { conditionBuilderManifests } from "./components/condition-builder/manifests.js";
import { switchCaseBuilderManifests } from "./components/switch-case-builder/manifests.js";
import { bindingPickerManifests } from "./components/binding-picker/manifests.js";
import { bindingTextBoxManifests } from "./components/binding-text-box/manifests.js";

export const manifests: UmbExtensionManifest[] = [
    ...versionHistoryManifests,
    ...conditionBuilderManifests,
    ...switchCaseBuilderManifests,
    ...bindingPickerManifests,
    ...bindingTextBoxManifests,
];
