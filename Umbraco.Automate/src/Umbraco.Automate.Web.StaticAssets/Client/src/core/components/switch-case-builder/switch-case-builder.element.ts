import { css, html, customElement, property, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UmbPropertyEditorUiElement, UmbPropertyEditorConfigCollection } from "@umbraco-cms/backoffice/property-editor";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import type { ConditionSet } from "../condition-builder/condition-builder.element.js";
import "../condition-builder/condition-builder.element.js";

export interface SwitchCase {
    Name: string;
    Conditions: ConditionSet;
}

function createEmptyCase(): SwitchCase {
    return {
        Name: "",
        Conditions: {
            Groups: [
                {
                    Conditions: [{ LeftOperand: "", Operator: "Equals", RightOperand: "" }],
                },
            ],
        },
    };
}

@customElement("ua-switch-case-builder")
export class UaSwitchCaseBuilderElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property({ attribute: false })
    value: SwitchCase[] = [];

    @property({ type: Array })
    bindingSources: BindingSource[] = [];

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        const sources = config.getValueByAlias<BindingSource[]>("bindingSources");
        if (sources) {
            this.bindingSources = sources;
        }
    }

    #cloneValue(): SwitchCase[] {
        return structuredClone(this.value);
    }

    #emitChange(newValue: SwitchCase[]) {
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onCaseNameChange(caseIndex: number, e: Event) {
        const input = e.target as HTMLInputElement;
        const newValue = this.#cloneValue();
        newValue[caseIndex].Name = input.value;
        this.#emitChange(newValue);
    }

    #onConditionsChange(caseIndex: number, e: Event) {
        e.stopPropagation();
        const conditionBuilder = e.target as HTMLElement & { value: ConditionSet };
        const newValue = this.#cloneValue();
        newValue[caseIndex].Conditions = conditionBuilder.value;
        this.#emitChange(newValue);
    }

    #addCase() {
        const newValue = this.#cloneValue();
        newValue.push(createEmptyCase());
        this.#emitChange(newValue);
    }

    #removeCase(caseIndex: number) {
        const newValue = this.#cloneValue();
        newValue.splice(caseIndex, 1);
        this.#emitChange(newValue);
    }

    #renderCase(caseItem: SwitchCase, caseIndex: number) {
        return html`
            <uui-box>
                <div class="case-header">
                    <uui-label>${this.localize.term("uaSwitchCaseBuilder_caseName")}</uui-label>
                    <div class="case-header-actions">
                        <uui-button
                            look="secondary"
                            compact
                            label=${this.localize.term("uaSwitchCaseBuilder_removeCase")}
                            @click=${() => this.#removeCase(caseIndex)}
                        >
                            <uui-icon name="icon-trash"></uui-icon>
                        </uui-button>
                    </div>
                </div>
                <uui-input
                    class="case-name-input"
                    label=${this.localize.term("uaSwitchCaseBuilder_caseName")}
                    placeholder=${this.localize.term("uaSwitchCaseBuilder_caseNamePlaceholder")}
                    .value=${caseItem.Name}
                    @change=${(e: Event) => this.#onCaseNameChange(caseIndex, e)}
                ></uui-input>
                <ua-condition-builder
                    .value=${caseItem.Conditions}
                    .bindingSources=${this.bindingSources}
                    @change=${(e: Event) => this.#onConditionsChange(caseIndex, e)}
                ></ua-condition-builder>
            </uui-box>
        `;
    }

    override render() {
        const cases = this.value ?? [];

        return html`
            <div class="switch-case-builder">
                ${cases.length > 0
                    ? repeat(
                          cases,
                          (_caseItem, index) => index,
                          (caseItem, index) => this.#renderCase(caseItem, index),
                      )
                    : nothing}

                <uui-button
                    class="add-case-btn"
                    look="placeholder"
                    label=${this.localize.term("uaSwitchCaseBuilder_addCase")}
                    @click=${() => this.#addCase()}
                >
                    ${this.localize.term("uaSwitchCaseBuilder_addCase")}
                </uui-button>

                <p class="default-info">
                    ${this.localize.term("uaSwitchCaseBuilder_defaultInfo")}
                </p>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            .switch-case-builder {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-4);
            }

            uui-box {
                --uui-box-default-padding: var(--uui-size-space-4);
            }

            .case-header {
                display: flex;
                align-items: center;
                justify-content: space-between;
                margin-bottom: var(--uui-size-space-3);
            }

            .case-name-input {
                width: 100%;
                margin-bottom: var(--uui-size-space-4);
            }

            .add-case-btn {
                margin-top: var(--uui-size-space-2);
            }

            .default-info {
                margin: var(--uui-size-space-2) 0 0;
                padding: 0;
                color: var(--uui-color-text-alt);
                font-size: var(--uui-type-small-size);
                font-style: italic;
            }
        `,
    ];
}

export default UaSwitchCaseBuilderElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-switch-case-builder": UaSwitchCaseBuilderElement;
    }
}
