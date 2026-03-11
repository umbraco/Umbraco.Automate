import type { ManifestMenuItem } from "@umbraco-cms/backoffice/menu";

export interface UaEntityContainerMenuItemManifest extends ManifestMenuItem {
    kind: "entityContainer";
    meta: ManifestMenuItem["meta"] & {
        childEntityTypes?: string[];
    };
}
