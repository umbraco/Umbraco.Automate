import { html, customElement, state, nothing, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement, UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import { UA_BINDING_PICKER_MODAL } from "../../../core/components/binding-text-box/binding-picker-modal.token.js";
import { UNARY_OPERATORS, type Condition, type ConditionOperator } from "../../../core/components/condition-builder/types.js";
import type { UaConditionModalData, UaConditionModalValue } from "./types.js";

const OPERATOR_OPTIONS: Array<{ name: string; value: ConditionOperator }> = [
    { name: "equals", value: "Equals" },
    { name: "not equals", value: "NotEquals" },
    { name: "contains", value: "Contains" },
    { name: "not contains", value: "NotContains" },
    { name: "starts with", value: "StartsWith" },
    { name: "ends with", value: "EndsWith" },
    { name: "greater than", value: "GreaterThan" },
    { name: "less than", value: "LessThan" },
    { name: ">=", value: "GreaterThanOrEquals" },
    { name: "<=", value: "LessThanOrEquals" },
    { name: "is empty", value: "IsEmpty" },
    { name: "is not empty", value: "IsNotEmpty" },
];

@customElement("ua-condition-modal")
export class UaConditionModalElement extends UmbModalBaseElement<UaConditionModalData, UaConditionModalValue> {
    @state()
    private _condition?: Condition;

    override connectedCallback() {
        super.connectedCallback();
        if (this.data?.condition) {
            this._condition = { ...this.data.condition };
        }
    }

    #onLeftChange(e: Event) {
        const input = e.target as HTMLInputElement;
        if (!this._condition) return;
        this._condition = { ...this._condition, LeftOperand: input.value };
    }

    #onOperatorChange(e: Event) {
        const select = e.target as HTMLSelectElement;
        if (!this._condition) return;
        const op = select.value as ConditionOperator;
        const next: Condition = { ...this._condition, Operator: op };
        if (UNARY_OPERATORS.includes(op)) next.RightOperand = "";
        this._condition = next;
    }

    #onRightChange(e: Event) {
        const input = e.target as HTMLInputElement;
        if (!this._condition) return;
        this._condition = { ...this._condition, RightOperand: input.value };
    }

    async #insertBinding(operand: "LeftOperand" | "RightOperand") {
        if (!this._condition || !this.data) return;
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return;

        const modal = modalManager.open(this, UA_BINDING_PICKER_MODAL, {
            data: { sources: this.data.bindingSources },
        });

        try {
            const { expression } = await modal.onSubmit();
            const current = this._condition[operand] ?? "";
            this._condition = { ...this._condition, [operand]: current + expression };
        } catch {
            // dismissed
        }
    }

    #onSubmit() {
        if (!this._condition) return;
        this.value = { condition: this._condition };
        this.modalContext?.submit();
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this._condition) return html``;
        const isUnary = UNARY_OPERATORS.includes(this._condition.Operator);
        const operatorOptions = OPERATOR_OPTIONS.map((o) => ({
            ...o,
            selected: o.value === this._condition!.Operator,
        }));
        const hasBindings = (this.data?.bindingSources.length ?? 0) > 0;

        return html`
            <umb-body-layout headline=${this.localize.term("uaConditionBuilder_editCondition")}>
                <div id="content">
                    <uui-box>
                        <umb-property-layout label=${this.localize.term("uaConditionBuilder_leftOperandPlaceholder")} orientation="vertical">
                            <div slot="editor">
                                ${this.#renderOperandInput(this._condition.LeftOperand, "LeftOperand", hasBindings)}
                            </div>
                        </umb-property-layout>

                        <umb-property-layout label=${this.localize.term("uaConditionBuilder_operator")} orientation="vertical">
                            <div slot="editor">
                                <uui-select
                                    .options=${operatorOptions}
                                    @change=${this.#onOperatorChange}
                                ></uui-select>
                            </div>
                        </umb-property-layout>

                        ${isUnary
                            ? nothing
                            : html`
                                  <umb-property-layout label=${this.localize.term("uaConditionBuilder_rightOperandPlaceholder")} orientation="vertical">
                                      <div slot="editor">
                                          ${this.#renderOperandInput(this._condition.RightOperand, "RightOperand", hasBindings)}
                                      </div>
                                  </umb-property-layout>
                              `}
                    </uui-box>
                </div>
                <div slot="actions">
                    <uui-button label=${this.localize.term("uaGeneral_close")} @click=${this.#onCancel}></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.localize.term("uaGeneral_save")}
                        @click=${this.#onSubmit}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    #renderOperandInput(value: string, operand: "LeftOperand" | "RightOperand", hasBindings: boolean) {
        const onInput = operand === "LeftOperand" ? this.#onLeftChange : this.#onRightChange;
        return html`
            <div class="operand-input">
                <uui-input
                    .value=${value ?? ""}
                    @input=${onInput.bind(this)}
                ></uui-input>
                ${hasBindings
                    ? html`<uui-button
                          look="outline"
                          compact
                          title=${this.localize.term("uaBindings_insertExpression")}
                          @click=${() => this.#insertBinding(operand)}
                      >
                          <uui-icon name="icon-code"></uui-icon>
                      </uui-button>`
                    : nothing}
            </div>
        `;
    }

    static override styles = [
        css`
            .operand-input {
                display: flex;
                gap: var(--uui-size-space-2);
            }

            .operand-input uui-input {
                flex: 1;
            }

            uui-select {
                width: 100%;
            }

            umb-property-layout[orientation="vertical"] {
                padding-bottom: 0;
            }

            umb-property-layout:first-of-type {
                padding-top: 0;
            }
        `,
    ];
}

export default UaConditionModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-condition-modal": UaConditionModalElement;
    }
}
