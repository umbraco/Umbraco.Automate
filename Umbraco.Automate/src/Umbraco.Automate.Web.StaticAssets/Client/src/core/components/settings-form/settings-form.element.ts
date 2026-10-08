import { css, html, customElement, nothing, property, repeat, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UmbPropertyValueData, UmbPropertyDatasetElement } from "@umbraco-cms/backoffice/property";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";
import type { BindingSource } from "../../utils/binding-context.utils.js";
import { escapeUfmExpressions } from "../../utils/ufm.utils.js";
import { BINDING_TEXT_BOX_UI_ALIAS } from "../binding-text-box/manifests.js";
import { BINDING_TEXT_AREA_UI_ALIAS } from "../binding-text-area/manifests.js";
import { BINDING_CODE_EDITOR_UI_ALIAS } from "../binding-code-editor/manifests.js";
import { SENSITIVE_FIELD_UI_ALIAS } from "../sensitive-field/manifests.js";

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

    /**
     * Id of the workspace that owns the automation being edited. Injected into every
     * field's editor config so workspace-scoped pickers (e.g. the automation picker)
     * can narrow their listing.
     */
    @property({ attribute: "workspace-id" })
    workspaceId?: string;

    @state()
    private _propertyValues: UmbPropertyValueData[] = [];

    /**
     * The values as currently edited, keyed by field. Drives which `visibleWhen` fields are
     * shown, so a change re-renders the form without re-populating from `values`.
     */
    @state()
    private _currentValues: Record<string, unknown> = {};

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

    // Populated before render rather than after, so the first render already knows the values
    // that decide which fields are visible. Populating in `updated` rendered every
    // `visibleWhen` field hidden first and then showed it, churning the property elements.
    override willUpdate(changedProperties: Map<string, unknown>) {
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
        this._currentValues = Object.fromEntries(this._propertyValues.map((v) => [v.alias, v.value]));
    }

    /**
     * A field with `visibleWhen` only applies while its controlling field holds one of the
     * listed values. Hidden fields are not rendered, so their mandatory validation does not
     * run either; their values stay in the dataset so switching back restores them.
     */
    #isVisible(field: EditableModelFieldDescriptorModel): boolean {
        const condition = field.visibleWhen;
        if (!condition) return true;

        // Dropdown editors store their selection as an array, even for a single choice.
        const raw = this._currentValues[condition.key];
        const current = Array.isArray(raw) ? raw[0] : raw;
        if (current === undefined || current === null) return false;

        const text = String(current).toLowerCase();
        return condition.values.some((v) => v.toLowerCase() === text);
    }

    #onChange(e: Event) {
        const dataset = e.target as UmbPropertyDatasetElement;
        const settings = dataset.value.reduce(
            (acc, curr) => ({ ...acc, [curr.alias]: curr.value }),
            {} as Record<string, unknown>,
        );

        this.#lastEmittedSettings = settings;

        // Keep the bound dataset value in step with the edit before re-rendering for visibility:
        // Lit re-assigns object property bindings on every render, so a stale `_propertyValues`
        // would push the initial values back into the dataset and undo the edit (e.g. a
        // key/value "Add" row vanishing straight after it was added).
        this._propertyValues = dataset.value;
        this._currentValues = settings;

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
            if (alias === "Umb.PropertyEditorUi.CodeEditor") {
                return BINDING_CODE_EDITOR_UI_ALIAS;
            }
            // A sensitive field the schema builder routed to the masked editor still has to
            // render the binding picker, so it takes the same swap. Bindings are pointers the
            // author has to read and choose, like configuration references, so the value shows
            // in clear here. Only reachable when binding sources are actually in scope; the
            // same field falls back to the masked editor everywhere else. Set an explicit
            // EditorUiAlias on the field to pin one editor across both cases.
            if (alias === SENSITIVE_FIELD_UI_ALIAS) {
                return BINDING_TEXT_BOX_UI_ALIAS;
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

        if (this.workspaceId) {
            config.push({ alias: "workspaceId", value: this.workspaceId });
        }

        return config;
    }

    @property({ type: Boolean, attribute: "label-on-top" })
    labelOnTop = false;

    #renderField(field: EditableModelFieldDescriptorModel) {
        // data-path lets the field's validators report client-side messages (e.g. mandatory)
        // to the hosting modal's UmbValidationContext so they render inline. This is a
        // client-only path — settings have no server-model validation, so it intentionally
        // does not match the CMS `$.values[...]` shape that server ModelState messages bind to.
        return html`
            <umb-property
                label=${this.localize.string(field.label)}
                description=${escapeUfmExpressions(this.localize.string(field.description ?? ""))}
                alias=${field.key}
                data-path=${`$.${field.key}`}
                property-editor-ui-alias=${this.#resolveEditorAlias(field)}
                .appearance=${{ labelOnTop: this.labelOnTop }}
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

    // Keyed by field, so showing or hiding a `visibleWhen` field never hands one field's
    // umb-property (and its live editor) to another. Positional reuse pushed a hidden field's
    // value into the next field's editor, e.g. Content Type's string into the Headers editor.
    #renderFields(fields: EditableModelFieldDescriptorModel[]) {
        return repeat(
            fields,
            (f) => f.key,
            (f) => this.#renderField(f),
        );
    }

    override render() {
        if (!this.fields.length) {
            return html`<div class="empty">
                <umb-localize key="uaSettings_noSettings">This item has no configurable settings.</umb-localize>
            </div>`;
        }

        const grouped = this.#groupFields(this.fields.filter((f) => this.#isVisible(f)));

        return html`
            <umb-property-dataset .value=${this._propertyValues} @change=${this.#onChange}>
                ${grouped.map((g) =>
                    this.noBox
                        ? html`${g.group ? html`<span class="group-headline">${this.localize.string(g.group)}</span>` : nothing}
                              ${this.#renderFields(g.fields)}`
                        : this.localize.string(g.group)
                          ? html`
                                <uui-box class="uui-text">
                                    <span slot="headline">${this.localize.string(g.group)}</span>
                                    ${this.#renderFields(g.fields)}
                                </uui-box>
                            `
                          : html`<uui-box class="uui-text">
                                ${this.#renderFields(g.fields)}
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

            uui-box + uui-box {
                margin-top: var(--uui-size-layout-2);
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
