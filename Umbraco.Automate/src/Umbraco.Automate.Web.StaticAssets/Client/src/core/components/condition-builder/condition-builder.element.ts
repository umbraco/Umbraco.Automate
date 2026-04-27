import { css, html, customElement, property, repeat, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import type { UmbPropertyEditorUiElement, UmbPropertyEditorConfigCollection } from "@umbraco-cms/backoffice/property-editor";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import { UA_CONDITION_MODAL } from "../../../automation/modals/condition/condition-modal.token.js";
import { UNARY_OPERATORS, type Condition, type ConditionGroup, type ConditionOperator, type ConditionSet } from "./types.js";

export type { Condition, ConditionGroup, ConditionOperator, ConditionSet };

const OPERATOR_LABELS: Record<ConditionOperator, string> = {
    Equals: "equals",
    NotEquals: "not equals",
    Contains: "contains",
    NotContains: "not contains",
    StartsWith: "starts with",
    EndsWith: "ends with",
    GreaterThan: ">",
    LessThan: "<",
    GreaterThanOrEquals: ">=",
    LessThanOrEquals: "<=",
    IsEmpty: "is empty",
    IsNotEmpty: "is not empty",
};

function createEmptyCondition(): Condition {
    return { LeftOperand: "", Operator: "Equals", RightOperand: "" };
}

function createEmptyGroup(): ConditionGroup {
    return { Conditions: [] };
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
        if (!Array.isArray(clone.Groups)) {
            clone.Groups = [createEmptyGroup()];
        }
        return clone;
    }

    #emitChange(newValue: ConditionSet) {
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    async #openConditionModal(initial: Condition): Promise<Condition | null> {
        const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
        if (!modalManager) return null;
        const modal = modalManager.open(this, UA_CONDITION_MODAL, {
            data: { condition: initial, bindingSources: this.bindingSources },
        });
        try {
            const { condition } = await modal.onSubmit();
            return condition;
        } catch {
            return null;
        }
    }

    async #addCondition(groupIndex: number) {
        const result = await this.#openConditionModal(createEmptyCondition());
        if (!result) return;
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions.push(result);
        this.#emitChange(newValue);
    }

    async #editCondition(groupIndex: number, conditionIndex: number) {
        const current = this.value.Groups[groupIndex].Conditions[conditionIndex];
        const result = await this.#openConditionModal(current);
        if (!result) return;
        const newValue = this.#cloneValue();
        newValue.Groups[groupIndex].Conditions[conditionIndex] = result;
        this.#emitChange(newValue);
    }

    #removeCondition(groupIndex: number, conditionIndex: number, e: Event) {
        e.stopPropagation();
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

    #summarise(condition: Condition): string {
        const op = OPERATOR_LABELS[condition.Operator];
        const left = condition.LeftOperand || "(empty)";
        if (UNARY_OPERATORS.includes(condition.Operator)) {
            return `${left} ${op}`;
        }
        const right = condition.RightOperand || "(empty)";
        return `${left} ${op} ${right}`;
    }

    #renderCondition(groupIndex: number, conditionIndex: number, condition: Condition) {
        return html`
            <uui-ref-node
                name=${this.#summarise(condition)}
                detail=${condition.LeftOperand || ""}
                @open=${() => this.#editCondition(groupIndex, conditionIndex)}
            >
                <uui-icon slot="icon" name="icon-filter"></uui-icon>
                <uui-action-bar slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_delete")}
                        @click=${(e: Event) => this.#removeCondition(groupIndex, conditionIndex, e)}
                    >
                        <uui-icon name="icon-trash"></uui-icon>
                    </uui-button>
                </uui-action-bar>
            </uui-ref-node>
        `;
    }

    #renderGroup(groupIndex: number, group: ConditionGroup) {
        const hasMultipleGroups = this.value.Groups.length > 1;
        return html`
            <div class="group">
                ${hasMultipleGroups
                    ? html`
                          <div class="group-header">
                              <uui-button
                                  look="secondary"
                                  compact
                                  label=${this.localize.term("uaConditionBuilder_removeGroup")}
                                  @click=${() => this.#removeGroup(groupIndex)}
                              >
                                  <uui-icon name="icon-trash"></uui-icon>
                              </uui-button>
                          </div>
                      `
                    : nothing}

                ${group.Conditions.length > 0
                    ? html`
                          <uui-ref-list>
                              ${repeat(
                                  group.Conditions,
                                  (_c, i) => `${groupIndex}-${i}`,
                                  (condition, index) => html`
                                      ${index > 0
                                          ? html`<span class="and-separator">${this.localize.term("uaConditionBuilder_and")}</span>`
                                          : nothing}
                                      ${this.#renderCondition(groupIndex, index, condition)}
                                  `,
                              )}
                          </uui-ref-list>
                      `
                    : html`<p class="empty-group">${this.localize.term("uaConditionBuilder_noConditions")}</p>`}

                <uui-button
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
                    (_g, i) => i,
                    (group, index) => html`
                        ${index > 0
                            ? html`<div class="or-separator">
                                  <uui-tag look="secondary" color="default">
                                      ${this.localize.term("uaConditionBuilder_or")}
                                  </uui-tag>
                              </div>`
                            : nothing}
                        ${this.#renderGroup(index, group)}
                    `,
                )}
                <uui-button
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
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
            }

            .group-header {
                display: flex;
                justify-content: flex-end;
            }

            uui-ref-list {
                display: block;
            }

            .empty-group {
                color: var(--uui-color-text-alt);
                font-style: italic;
                margin: 0;
                text-align: center;
            }

            .and-separator {
                display: block;
                text-align: center;
                font-size: var(--uui-type-small-size);
                font-weight: 600;
                color: var(--uui-color-text-alt);
                text-transform: uppercase;
                letter-spacing: 0.05em;
                padding: var(--uui-size-space-1) 0;
            }

            .or-separator {
                display: flex;
                align-items: center;
                justify-content: center;
                padding: var(--uui-size-space-2) 0;
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
