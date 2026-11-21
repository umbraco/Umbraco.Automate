import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import { UaCatalogueRepository } from "../../../catalogue/repository/catalogue.repository.js";
import type { UaConnectionTypeCatalogueItemModel } from "../../../catalogue/types.js";

/**
 * Property editor UI that selects a registered connection type by its alias, used by settings
 * fields that filter on a connection type (e.g. the Get Connections action). An empty value
 * means "any connection type".
 */
@customElement("ua-connection-type-picker")
export class UaConnectionTypePickerElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    @state()
    private _connectionTypes: UaConnectionTypeCatalogueItemModel[] = [];

    @state()
    private _loading = true;

    @state()
    private _error?: string;

    #catalogueRepo = new UaCatalogueRepository(this);

    protected override firstUpdated() {
        void this.#load();
    }

    async #load() {
        this._loading = true;
        this._error = undefined;

        try {
            const { data, error } = await this.#catalogueRepo.requestConnectionTypes();

            if (error || !data) {
                this._error = "Could not load connection types.";
                return;
            }

            this._connectionTypes = [...data].sort((a, b) => a.name.localeCompare(b.name));
        } catch {
            this._error = "Could not load connection types.";
        } finally {
            this._loading = false;
        }
    }

    #buildOptions() {
        const options = [
            { name: "All connection types", value: "", selected: !this.value },
            ...this._connectionTypes.map((type) => ({
                name: type.name,
                value: type.alias,
                selected: type.alias === this.value,
            })),
        ];

        // Keep a saved alias whose type is no longer registered (e.g. its package was removed)
        // visible instead of silently blanking the setting on save.
        if (this.value && !this._connectionTypes.some((type) => type.alias === this.value)) {
            options.push({
                name: `Missing connection type (${this.value})`,
                value: this.value,
                selected: true,
            });
        }

        return options;
    }

    #onChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        if (next === (this.value ?? "")) return;
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    override render() {
        if (this._loading) {
            return html`<uui-loader-bar></uui-loader-bar>`;
        }

        if (this._error) {
            return html`<div class="error">${this._error}</div>`;
        }

        return html`
            <uui-select
                label="Connection type"
                .options=${this.#buildOptions()}
                ?disabled=${this.readonly}
                @change=${this.#onChange}
            ></uui-select>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            uui-select {
                width: 100%;
            }

            .error {
                color: var(--uui-color-danger);
            }
        `,
    ];
}

export default UaConnectionTypePickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-type-picker": UaConnectionTypePickerElement;
    }
}
