import { html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { UaEdgeFilterModalData, UaEdgeFilterModalValue } from "./edge-filter-modal.token.js";
import type { ConditionSet, ConditionOperator } from "../../../core/components/condition-builder/condition-builder.element.js";
import type { ConditionSetModel } from "../../workspace/automation/canvas/types.js";
import "../../../core/components/condition-builder/condition-builder.element.js";

function toConditionSet(model: ConditionSetModel | null): ConditionSet {
    if (!model || !model.groups?.length) {
        return { Groups: [{ Conditions: [{ LeftOperand: "", Operator: "Equals", RightOperand: "" }] }] };
    }
    return {
        Groups: model.groups.map((g) => ({
            Conditions: g.conditions.map((c) => ({
                LeftOperand: c.leftOperand,
                Operator: c.operator as ConditionOperator,
                RightOperand: c.rightOperand,
            })),
        })),
    };
}

function toConditionSetModel(set: ConditionSet): ConditionSetModel | null {
    const groups = set.Groups.filter((g) =>
        g.Conditions.some((c) => c.LeftOperand || c.RightOperand),
    );
    if (groups.length === 0) return null;
    return {
        groups: groups.map((g) => ({
            conditions: g.Conditions.map((c) => ({
                leftOperand: c.LeftOperand,
                operator: c.Operator,
                rightOperand: c.RightOperand,
            })),
        })),
    };
}

@customElement("ua-edge-filter-modal")
export class UaEdgeFilterModalElement extends UmbModalBaseElement<
    UaEdgeFilterModalData,
    UaEdgeFilterModalValue
> {
    @state()
    private _conditionSet: ConditionSet = toConditionSet(null);

    override connectedCallback() {
        super.connectedCallback();
        this._conditionSet = toConditionSet(this.data?.filter ?? null);
    }

    #onChange() {
        this.requestUpdate();
    }

    #onSubmit() {
        this.value = { filter: toConditionSetModel(this._conditionSet) };
        this.modalContext?.submit();
    }

    #onRemove() {
        this.value = { filter: null };
        this.modalContext?.submit();
    }

    #onClose() {
        this.modalContext?.reject();
    }

    override render() {
        return html`
            <umb-body-layout headline=${this.localize.term("uaEdgeFilter_headline")}>
                <ua-condition-builder
                    .value=${this._conditionSet}
                    @change=${this.#onChange}
                ></ua-condition-builder>
                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaEdgeFilter_removeFilter")}
                        color="danger"
                        look="secondary"
                        @click=${this.#onRemove}
                    ></uui-button>
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onClose}
                    ></uui-button>
                    <uui-button
                        label=${this.localize.term("uaGeneral_save")}
                        look="primary"
                        color="positive"
                        @click=${this.#onSubmit}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }
}

export default UaEdgeFilterModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-edge-filter-modal": UaEdgeFilterModalElement;
    }
}
