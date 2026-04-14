import { css, html, customElement, nothing, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UmbPropertyValueData, UmbPropertyDatasetElement } from "@umbraco-cms/backoffice/property";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import { BINDING_TEXT_BOX_UI_ALIAS } from "../binding-text-box/manifests.js";
import { BINDING_TEXT_AREA_UI_ALIAS } from "../binding-text-area/manifests.js";

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

    @property({ type: Boolean, attribute: "no-box" })
    noBox = false;

    @property({ type: Array })
    bindingSources: BindingSource[] = [];

    @state()
    private _propertyValues: UmbPropertyValueData[] = [];

    /**
     * Tracks whether the initial population has been done for the current fields.
     * Prevents re-populating on every values change which would reset cursor position.
     */
    #isInitialized = false;

    /**
     * Tracks the last settings we emitted via the change event.
     * Used to distinguish echo updates from external changes.
     */
    #lastEmittedSettings: Record<string, unknown> | null = null;

    override shouldUpdate(changedProperties: Map<string, unknown>): boolean {
        if (this.#isInitialized && changedProperties.size === 1 && changedProperties.has("values")) {
            if (this.#isEchoUpdate(this.values)) {
                return false;
            }
            this.#isInitialized = false;
        }
        return true;
    }

    #isEchoUpdate(incoming: Record<string, unknown> | undefined): boolean {
        if (!this.#lastEmittedSettings || !incoming) {
            return false;
        }

        const lastKeys = Object.keys(this.#lastEmittedSettings);
        const incomingKeys = Object.keys(incoming);

        if (lastKeys.length !== incomingKeys.length) {
            return false;
        }

        return lastKeys.every((key) => this.#lastEmittedSettings![key] === incoming[key]);
    }

    override updated(changedProperties: Map<string, unknown>) {
        if (changedProperties.has("fields")) {
            this.#isInitialized = false;
            this.#lastEmittedSettings = null;
        }

        if (!this.#isInitialized && this.fields.length > 0) {
            this.#populatePropertyValues();
            this.#isInitialized = true;
        }
    }

    #populatePropertyValues() {
        this._propertyValues = this.fields.map((field) => ({
            alias: field.key,
            value: this.values?.[field.key] ?? field.defaultValue,
        }));
    }

    #onChange(e: Event) {
        const dataset = e.target as UmbPropertyDatasetElement;
        const settings = dataset.value.reduce(
            (acc, curr) => ({ ...acc, [curr.alias]: curr.value }),
            {} as Record<string, unknown>,
        );

        this.#lastEmittedSettings = settings;

        this.dispatchEvent(
            new CustomEvent<SettingsChangeDetail>("ua:settings-change", {
                detail: { settings },
                bubbles: true,
                composed: true,
            }),
        );
    }

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
        if (typeof config === "string") {
            try {
                return this.#toPropertyConfig(JSON.parse(config));
            } catch {
                return [];
            }
        }
        if (Array.isArray(config)) return config as Array<{ alias: string; value: unknown }>;
        if (typeof config !== "object") return [];
        return Object.entries(config).map(([alias, value]) => ({ alias, value }));
    }

    #resolveEditorAlias(field: EditableModelFieldDescriptorModel): string {
        // When bindings are available and the field supports them, swap the default
        // TextBox for our binding-aware variant that shows the picker button inline.
        if (field.supportsBindings && this.bindingSources.length > 0) {
            const alias = field.editorUiAlias ?? "Umb.PropertyEditorUi.TextBox";
            if (alias === "Umb.PropertyEditorUi.TextBox") {
                return BINDING_TEXT_BOX_UI_ALIAS;
            }
            if (alias === "Umb.PropertyEditorUi.TextArea") {
                return BINDING_TEXT_AREA_UI_ALIAS;
            }
        }
        return field.editorUiAlias ?? "Umb.PropertyEditorUi.TextBox";
    }

    #buildFieldConfig(field: EditableModelFieldDescriptorModel): Array<{ alias: string; value: unknown }> {
        const config = field.editorConfig ? this.#toPropertyConfig(field.editorConfig) : [];

        // Inject binding sources into the property editor config so the
        // binding text box can render its picker button.
        if (field.supportsBindings && this.bindingSources.length > 0) {
            config.push({ alias: "bindingSources", value: this.bindingSources });
        }

        return config;
    }

    #renderField(field: EditableModelFieldDescriptorModel) {
        return html`
            <umb-property
                label=${this.localize.string(field.label)}
                description=${field.description ?? ""}
                alias=${field.key}
                property-editor-ui-alias=${this.#resolveEditorAlias(field)}
                .appearance=${{ labelOnTop: true }}
                .config=${this.#buildFieldConfig(field)}
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
            <umb-property-dataset .value=${this._propertyValues} @change=${this.#onChange}>
                ${grouped.map((g) =>
                    this.noBox
                        ? html`${g.group ? html`<span class="group-headline">${g.group}</span>` : nothing}
                              ${g.fields.map((f) => this.#renderField(f))}`
                        : g.group
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
            </umb-property-dataset>
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

            umb-property {
                --uui-size-layout-1: var(--uui-size-space-2);
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
