import { css, html, customElement, property, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UmbPropertyEditorUiElement, UmbPropertyEditorConfigCollection } from "@umbraco-cms/backoffice/property-editor";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { BindingSource } from "../../utils/binding-context.utils.js";

export type ConditionOperator =
    | "Equals"
    | "NotEquals"
    | "Contains"
    | "NotContains"
    | "StartsWith"
    | "EndsWith"
    | "GreaterThan"
    | "LessThan"
    | "GreaterThanOrEquals"
    | "LessThanOrEquals"
    | "IsEmpty"
    | "IsNotEmpty";

export interface Condition {
    LeftOperand: string;
    Operator: ConditionOperator;
    RightOperand: string;
}

export interface ConditionGroup {
    Conditions: Condition[];
}

export interface ConditionSet {
    Groups: ConditionGroup[];
}

const OPERATOR_OPTIONS = [
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

const UNARY_OPERATORS: ConditionOperator[] = ["IsEmpty", "IsNotEmpty"];

function createEmptyCondition(): Condition {
    return { LeftOperand: "", Operator: "Equals", RightOperand: "" };
}

function createEmptyGroup(): ConditionGroup {
    return { Conditions: [createEmptyCondition()] };
}

function createDefaultValue(): ConditionSet {
    return { Groups: [createEmptyGroup()] };
}

@customElement("ua-condition-builder")
export class UaConditionBuilderElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    private _value: ConditionSet = createDefaultValue();

    @property({ attribute: false })
    set value(val: ConditionSet) {
        this._value = val && Array.isArray(val.Groups) ? val : createDefaultValue();
    }

    get value(): ConditionSet {
        return this._value;
    }

    @property({ type: Array })
    bindingSources: BindingSource[] = [];

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        const sources = config.getValueByAlias<BindingSource[]>("bindingSources");
        if (sources) {
            this.bindingSources = sources;
        }
    }

    #cloneValue(): ConditionSet {
        const val = this.value ?? createDefaultValue();
        const clone = structuredClone(val);
        // Normalize: ensure Groups array always exists
        if (!Array.isArray(clone.Groups)) {
            clone.Groups = [createEmptyGroup()];
        }
        return clone;
    }

    #emitChange(newValue: ConditionSet) {
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onLeftOperandChange(groupIndex: number, conditionIndex: number, e: Event) {
        const input = e.target as HTMLInputElement;
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions[conditionIndex].LeftOperand = input.value;
        this.#emitChange(newValue);
    }

    #onOperatorChange(groupIndex: number, conditionIndex: number, e: Event) {
        const select = e.target as HTMLSelectElement & { value: string };
        const newValue = this.#cloneValue();
        const condition = newValue.Groups[groupIndex].Conditions[conditionIndex];
        condition.Operator = select.value as ConditionOperator;
        if (UNARY_OPERATORS.includes(condition.Operator)) {
            condition.RightOperand = "";
        }
        this.#emitChange(newValue);
    }

    #onRightOperandChange(groupIndex: number, conditionIndex: number, e: Event) {
        const input = e.target as HTMLInputElement;
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions[conditionIndex].RightOperand = input.value;
        this.#emitChange(newValue);
    }

    #addCondition(groupIndex: number) {
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions.push(createEmptyCondition());
        this.#emitChange(newValue);
    }

    #removeCondition(groupIndex: number, conditionIndex: number) {
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions.splice(conditionIndex, 1);
        this.#emitChange(newValue);
    }

    #addGroup() {
        const newValue = this.#cloneValue();
        newValue.Groups.push(createEmptyGroup());
        this.#emitChange(newValue);
    }

    #removeGroup(groupIndex: number) {
        const newValue = this.#cloneValue();
        newValue.Groups.splice(groupIndex, 1);
        this.#emitChange(newValue);
    }

    #getBindingOptions(selectedValue: string): Array<{ name: string; value: string; selected: boolean }> {
        const options: Array<{ name: string; value: string; selected: boolean }> = [
            { name: this.localize.term("uaConditionBuilder_selectBinding"), value: "", selected: !selectedValue },
        ];

        for (const source of this.bindingSources) {
            for (const leaf of source.leaves) {
                const expression = `\${ ${source.bindingPrefix}.${leaf.path} }`;
                options.push({
                    name: `${source.label} — ${leaf.path}`,
                    value: expression,
                    selected: expression === selectedValue,
                });
            }
        }

        return options;
    }

    #renderCondition(groupIndex: number, conditionIndex: number, condition: Condition, conditionCount: number) {
        const isUnary = UNARY_OPERATORS.includes(condition.Operator);
        const operatorOptions = OPERATOR_OPTIONS.map((opt) => ({
            ...opt,
            selected: opt.value === condition.Operator,
        }));

        const hasBindingSources = this.bindingSources.length > 0;

        return html`
            <div class="condition-row">
                ${hasBindingSources
                    ? html`<uui-select
                          class="operand-input"
                          label=${this.localize.term("uaConditionBuilder_leftOperandPlaceholder")}
                          .options=${this.#getBindingOptions(condition.LeftOperand)}
                          @change=${(e: Event) => this.#onLeftOperandChange(groupIndex, conditionIndex, e)}
                      ></uui-select>`
                    : html`<uui-input
                          class="operand-input"
                          placeholder=${this.localize.term("uaConditionBuilder_leftOperandPlaceholder")}
                          .value=${condition.LeftOperand}
                          @change=${(e: Event) => this.#onLeftOperandChange(groupIndex, conditionIndex, e)}
                      ></uui-input>`}

                <uui-select
                    class="operator-select"
                    .options=${operatorOptions}
                    @change=${(e: Event) => this.#onOperatorChange(groupIndex, conditionIndex, e)}
                ></uui-select>

                ${isUnary
                    ? nothing
                    : html`
                          <uui-input
                              class="operand-input"
                              placeholder=${this.localize.term("uaConditionBuilder_rightOperandPlaceholder")}
                              .value=${condition.RightOperand}
                              @change=${(e: Event) => this.#onRightOperandChange(groupIndex, conditionIndex, e)}
                          ></uui-input>
                      `}

                ${conditionCount > 1
                    ? html`
                          <uui-button
                              class="remove-btn"
                              look="secondary"
                              compact
                              label=${this.localize.term("uaConditionBuilder_removeCondition")}
                              @click=${() => this.#removeCondition(groupIndex, conditionIndex)}
                          >
                              <uui-icon name="icon-trash"></uui-icon>
                          </uui-button>
                      `
                    : nothing}
            </div>
        `;
    }

    #renderGroup(groupIndex: number, group: ConditionGroup) {
        return html`
            <div class="group">
                <div class="group-header">
                    ${this.value.Groups.length > 1
                        ? html`
                              <uui-button
                                  class="remove-group-btn"
                                  look="secondary"
                                  compact
                                  label=${this.localize.term("uaConditionBuilder_removeGroup")}
                                  @click=${() => this.#removeGroup(groupIndex)}
                              >
                                  <uui-icon name="icon-trash"></uui-icon>
                              </uui-button>
                          `
                        : nothing}
                </div>
                <div class="conditions">
                    ${repeat(
                        group.Conditions,
                        (_condition, index) => `${groupIndex}-${index}`,
                        (condition, index) => html`
                            ${index > 0 ? html`<span class="and-separator">${this.localize.term("uaConditionBuilder_and")}</span>` : nothing}
                            ${this.#renderCondition(groupIndex, index, condition, group.Conditions.length)}
                        `,
                    )}
                </div>
                <uui-button
                    class="add-condition-btn"
                    look="placeholder"
                    label=${this.localize.term("uaConditionBuilder_addCondition")}
                    @click=${() => this.#addCondition(groupIndex)}
                >
                    ${this.localize.term("uaConditionBuilder_addCondition")}
                </uui-button>
            </div>
        `;
    }

    override render() {
        const groups = this.value?.Groups ?? [];

        return html`
            <div class="condition-builder">
                ${repeat(
                    groups,
                    (_group, index) => index,
                    (group, index) => html`
                        ${index > 0 ? html`<div class="or-separator"><uui-tag look="secondary" color="default">${this.localize.term("uaConditionBuilder_or")}</uui-tag></div>` : nothing}
                        ${this.#renderGroup(index, group)}
                    `,
                )}
                <uui-button
                    class="add-group-btn"
                    look="placeholder"
                    label=${this.localize.term("uaConditionBuilder_addGroup")}
                    @click=${() => this.#addGroup()}
                >
                    ${this.localize.term("uaConditionBuilder_addGroup")}
                </uui-button>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            .condition-builder {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
            }

            .group {
                border: 1px solid var(--uui-color-border);
                border-radius: var(--uui-border-radius);
                padding: var(--uui-size-space-4);
                background: var(--uui-color-surface);
            }

            .group-header {
                display: flex;
                justify-content: flex-end;
                margin-bottom: var(--uui-size-space-2);
            }

            .group-header:empty {
                display: none;
            }

            .conditions {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
            }

            .condition-row {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
            }

            .operand-input,
            .operator-select {
                flex: 1;
            }

            .remove-btn,
            .remove-group-btn {
                flex-shrink: 0;
            }

            .and-separator {
                display: block;
                text-align: center;
                font-size: var(--uui-type-small-size);
                font-weight: 600;
                color: var(--uui-color-text-alt);
                text-transform: uppercase;
                letter-spacing: 0.05em;
            }

            .or-separator {
                display: flex;
                align-items: center;
                justify-content: center;
                padding: var(--uui-size-space-2) 0;
            }

            .add-condition-btn {
                margin-top: var(--uui-size-space-3);
            }

            .add-group-btn {
                margin-top: var(--uui-size-space-2);
            }
        `,
    ];
}

export default UaConditionBuilderElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-condition-builder": UaConditionBuilderElement;
    }
}
