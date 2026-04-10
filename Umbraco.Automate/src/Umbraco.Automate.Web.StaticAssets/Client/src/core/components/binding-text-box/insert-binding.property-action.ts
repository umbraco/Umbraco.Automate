import { UmbPropertyActionBase } from "@umbraco-cms/backoffice/property-action";
import { UMB_PROPERTY_CONTEXT } from "@umbraco-cms/backoffice/property";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UA_BINDING_PICKER_MODAL } from "./binding-picker-modal.token.js";
import type { BindingSource } from "../../utils/binding-context.utils.js";

/**
 * Property action that opens the binding picker modal and inserts the
 * selected `${ }` expression into the property value.
 */
export class UaInsertBindingPropertyAction extends UmbPropertyActionBase {
    override async execute() {
        const propertyContext = await this.getContext(UMB_PROPERTY_CONTEXT);
        if (!propertyContext) return;

        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        // Read binding sources from property config (injected by settings form).
        const configArray = propertyContext.getConfig();
        const sourcesEntry = configArray?.find((c) => c.alias === "bindingSources");
        const bindingSources = (sourcesEntry?.value as BindingSource[] | undefined) ?? [];

        if (bindingSources.length === 0) return;

        const modal = modalManager.open(this._host, UA_BINDING_PICKER_MODAL, {
            data: { sources: bindingSources },
        });

        try {
            const { expression } = await modal.onSubmit();
            const currentValue = (propertyContext.getValue() as string) ?? "";
            propertyContext.setValue(currentValue + expression);
        } catch {
            // Modal dismissed
        }
    }
}

export default UaInsertBindingPropertyAction;
