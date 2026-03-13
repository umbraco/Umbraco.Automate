import { css, html, customElement, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";

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

    #toPropertyConfig(config: unknown): Array<{ alias: string; value: unknown }> {
        if (!config) return [];
        // If it's already an array of alias-value pairs, return as is
        if (Array.isArray(config)) return config as Array<{ alias: string; value: unknown }>;
        // If it's an object, convert its entries to alias-value pairs
        if (typeof config !== "object") return [];
        return Object.entries(config).map(([alias, value]) => ({ alias, value }));
    }

    #renderField(field: EditableModelFieldDescriptorModel) {
        return html`
            <umb-property
                label=${this.localize.string(field.label)}
                description=${this.localize.string(field.description ?? "")}
                alias=${field.key}
                property-editor-ui-alias=${field.editorUiAlias ?? "Umb.PropertyEditorUi.TextBox"}
                .config=${field.editorConfig ? this.#toPropertyConfig(field.editorConfig) : []}
                .validation=${{
                mandatory: field.isRequired,
                mandatoryMessage: field.isRequired
                    ? this.localize.string("This field is required")
                    : undefined,
            }}
            >
            </umb-property>
        `;
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
