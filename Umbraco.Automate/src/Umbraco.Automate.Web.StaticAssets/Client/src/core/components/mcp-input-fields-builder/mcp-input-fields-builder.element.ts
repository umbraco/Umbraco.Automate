import { css, customElement, html, property, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";

/**
 * One row edited by {@link UaMcpInputFieldsBuilderElement}. Mirrors
 * `McpToolInputField` (Umbraco.Automate.Core.Triggers.BuiltIn) field-for-field.
 * Settings JSON from the frontend is camelCase and bridged to the PascalCase
 * POCO by case-insensitive matching (see `JsonOptions.Settings`).
 */
export interface McpToolInputFieldRow {
    name: string;
    type: "Text" | "Number" | "Boolean";
    description: string | null;
    required: boolean;
}

function createEmptyField(): McpToolInputFieldRow {
    return { name: "", type: "Text", description: null, required: false };
}

@customElement("ua-mcp-input-fields-builder")
export class UaMcpInputFieldsBuilderElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property({ attribute: false })
    value: McpToolInputFieldRow[] = [];

    #cloneValue(): McpToolInputFieldRow[] {
        return structuredClone(this.value ?? []);
    }

    #emitChange(newValue: McpToolInputFieldRow[]) {
        this.value = newValue;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #addField() {
        const next = this.#cloneValue();
        next.push(createEmptyField());
        this.#emitChange(next);
    }

    #removeField(index: number) {
        const next = this.#cloneValue();
        next.splice(index, 1);
        this.#emitChange(next);
    }

    #updateField(index: number, patch: Partial<McpToolInputFieldRow>) {
        const next = this.#cloneValue();
        next[index] = { ...next[index], ...patch };
        this.#emitChange(next);
    }

    override render() {
        return html`
            <div class="fields">
                ${repeat(
                    this.value ?? [],
                    (_field, index) => index,
                    (field, index) => html`
                        <uui-box class="field">
                            <div class="field-header">
                                <uui-input
                                    class="name-input"
                                    label="Name"
                                    placeholder="Argument name"
                                    .value=${field.name}
                                    @input=${(e: InputEvent) => this.#updateField(index, { name: (e.target as HTMLInputElement).value })}
                                ></uui-input>
                                <uui-select
                                    class="type-select"
                                    .options=${[
                                        { name: "Text", value: "Text", selected: field.type === "Text" },
                                        { name: "Number", value: "Number", selected: field.type === "Number" },
                                        { name: "Boolean", value: "Boolean", selected: field.type === "Boolean" },
                                    ]}
                                    @change=${(e: CustomEvent) =>
                                        this.#updateField(index, {
                                            type: (e.target as HTMLSelectElement).value as McpToolInputFieldRow["type"],
                                        })}
                                ></uui-select>
                                <uui-button compact look="secondary" label="Remove field" @click=${() => this.#removeField(index)}>
                                    <uui-icon name="icon-trash"></uui-icon>
                                </uui-button>
                            </div>
                            <uui-input
                                class="description-input"
                                label="Description"
                                placeholder="Tells the agent what this argument is for"
                                .value=${field.description ?? ""}
                                @input=${(e: InputEvent) => this.#updateField(index, { description: (e.target as HTMLInputElement).value })}
                            ></uui-input>
                            <uui-toggle
                                label="Required"
                                ?checked=${field.required}
                                @change=${(e: Event) => this.#updateField(index, { required: (e.target as HTMLInputElement).checked })}
                            ></uui-toggle>
                        </uui-box>
                    `,
                )}
            </div>
            <uui-button look="secondary" label="Add field" @click=${() => this.#addField()}>
                <uui-icon name="icon-add"></uui-icon>
                Add field
            </uui-button>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            .fields {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
                margin-bottom: var(--uui-size-space-3);
            }

            uui-box {
                --uui-box-default-padding: var(--uui-size-space-4);
            }

            .field {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
            }

            .field-header {
                display: flex;
                align-items: flex-end;
                gap: var(--uui-size-space-3);
            }

            .name-input {
                flex: 1;
            }

            .type-select {
                width: 8rem;
                flex: none;
            }

            .description-input {
                width: 100%;
                margin-top: var(--uui-size-space-2);
            }
        `,
    ];
}

export default UaMcpInputFieldsBuilderElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-mcp-input-fields-builder": UaMcpInputFieldsBuilderElement;
    }
}
