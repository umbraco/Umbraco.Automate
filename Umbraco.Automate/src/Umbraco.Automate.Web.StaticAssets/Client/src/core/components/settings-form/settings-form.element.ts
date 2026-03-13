import { css, html, customElement, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";
import { resolveEditorType, type FieldEditorType } from "./field-mapper.js";

export interface SettingsChangeDetail {
    settings: Record<string, unknown>;
}

interface GroupedFields {
    group: string;
    fields: EditableModelFieldDescriptorModel[];
}

@customElement("ua-settings-form")
export class UaSettingsFormElement extends UmbLitElement {
    @property({ type: Array })
    fields: EditableModelFieldDescriptorModel[] = [];

    @property({ type: Object })
    values: Record<string, unknown> = {};

    #groupFields(fields: EditableModelFieldDescriptorModel[]): GroupedFields[] {
        const sorted = [...fields].sort((a, b) => a.sortOrder - b.sortOrder);
        const groups = new Map<string, EditableModelFieldDescriptorModel[]>();

        for (const field of sorted) {
            const group = field.group || "";
            if (!groups.has(group)) {
                groups.set(group, []);
            }
            groups.get(group)!.push(field);
        }

        return Array.from(groups.entries()).map(([group, fields]) => ({ group, fields }));
    }

    #onFieldChange(propertyName: string, value: unknown) {
        const updated = { ...this.values, [propertyName]: value };
        this.dispatchEvent(
            new CustomEvent<SettingsChangeDetail>("ua:settings-change", {
                bubbles: true,
                composed: true,
                detail: { settings: updated },
            }),
        );
    }

    #renderField(field: EditableModelFieldDescriptorModel) {
        const editorType = resolveEditorType(field.editorUiAlias, field.propertyType, field.isSensitive);
        const value = this.values[field.propertyName];

        return html`
            <umb-property-layout
                label=${field.label}
                orientation="vertical"
                ?mandatory=${field.isRequired}
            >
                ${field.description
                    ? html`<small slot="description">${field.description}</small>`
                    : ""}
                <div slot="editor">
                    ${this.#renderEditor(field, editorType, value)}
                </div>
            </umb-property-layout>
        `;
    }

    #renderEditor(
        field: EditableModelFieldDescriptorModel,
        editorType: FieldEditorType,
        value: unknown,
    ) {
        switch (editorType) {
            case "toggle":
                return html`
                    <uui-toggle
                        .checked=${Boolean(value)}
                        label=${field.label}
                        @change=${(e: Event) =>
                            this.#onFieldChange(field.propertyName, (e.target as HTMLInputElement).checked)}
                    ></uui-toggle>
                `;
            case "textarea":
                return html`
                    <uui-textarea
                        .value=${String(value ?? "")}
                        ?required=${field.isRequired}
                        label=${field.label}
                        auto-height
                        @input=${(e: Event) =>
                            this.#onFieldChange(field.propertyName, (e.target as HTMLTextAreaElement).value)}
                    ></uui-textarea>
                `;
            case "number":
                return html`
                    <uui-input
                        type="number"
                        .value=${value != null ? String(value) : ""}
                        ?required=${field.isRequired}
                        label=${field.label}
                        @input=${(e: Event) => {
                            const raw = (e.target as HTMLInputElement).value;
                            this.#onFieldChange(field.propertyName, raw === "" ? null : Number(raw));
                        }}
                    ></uui-input>
                `;
            case "password":
                return html`
                    <uui-input
                        type="password"
                        .value=${String(value ?? "")}
                        ?required=${field.isRequired}
                        label=${field.label}
                        @input=${(e: Event) =>
                            this.#onFieldChange(field.propertyName, (e.target as HTMLInputElement).value)}
                    ></uui-input>
                `;
            default:
                return html`
                    <uui-input
                        .value=${String(value ?? "")}
                        ?required=${field.isRequired}
                        label=${field.label}
                        @input=${(e: Event) =>
                            this.#onFieldChange(field.propertyName, (e.target as HTMLInputElement).value)}
                    ></uui-input>
                `;
        }
    }

    override render() {
        if (!this.fields.length) {
            return html`<div class="empty">
                <umb-localize key="uaSettings_noSettings">This item has no configurable settings.</umb-localize>
            </div>`;
        }

        const grouped = this.#groupFields(this.fields);

        return html`
            ${grouped.map((g) =>
                g.group
                    ? html`
                          <uui-box class="uui-text">
                              <span slot="headline">${g.group}</span>
                              ${g.fields.map((f) => this.#renderField(f))}
                          </uui-box>
                      `
                    : html`<uui-box class="uui-text">
                          ${g.fields.map((f) => this.#renderField(f))}
                      </uui-box>`,
            )}
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            umb-property-layout[orientation="vertical"] {
                padding: var(--uui-size-space-2) 0;
            }

            umb-property-layout:first-of-type {
                padding-top: 0;
            }

            umb-property-layout:last-of-type {
                padding-bottom: 0;
            }

            umb-property-layout [slot="description"] {
                display: block;
            }

            uui-input,
            uui-textarea,
            uui-select {
                width: 100%;
            }

            uui-input:focus-within {
                z-index: 1;
            }

            .empty {
                color: var(--uui-color-text-alt);
                font-style: italic;
            }
        `,
    ];
}

export default UaSettingsFormElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-settings-form": UaSettingsFormElement;
    }
}
