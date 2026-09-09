import { css, customElement, html, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import type {
    UmbPropertyEditorConfigCollection,
    UmbPropertyEditorUiElement,
} from "@umbraco-cms/backoffice/property-editor";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import { AutomationsService } from "../../../api/sdk.gen.js";
import type { AutomationItemResponseModel } from "../../../api/types.gen.js";

/**
 * Property editor UI that selects an automation by its unique id, used by settings
 * fields that reference another automation (e.g. the Start Automation action).
 * When the hosting settings form provides a `workspaceId` config value the list is
 * scoped to that workspace; otherwise it lists every automation the user can access.
 */
@customElement("ua-automation-picker")
export class UaAutomationPickerElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    public set config(config: UmbPropertyEditorConfigCollection | undefined) {
        if (!config) return;
        this._workspaceId = config.getValueByAlias<string>("workspaceId");
    }

    @state()
    private _workspaceId?: string;

    @state()
    private _automations: AutomationItemResponseModel[] = [];

    @state()
    private _loading = true;

    @state()
    private _error?: string;

    // Load after first update so the config (workspace scope) has been assigned.
    protected override firstUpdated() {
        void this.#load();
    }

    async #load() {
        this._loading = true;
        this._error = undefined;

        try {
            const { data, error } = await AutomationsService.getAutomations({
                query: { workspaceId: this._workspaceId, take: 1000 },
            });

            if (error || !data) {
                this._error = "Could not load automations.";
                return;
            }

            this._automations = [...data.items].sort((a, b) => a.name.localeCompare(b.name));
        } catch {
            this._error = "Could not load automations.";
        } finally {
            this._loading = false;
        }
    }

    #buildOptions() {
        const options = [
            { name: "Select an automation…", value: "", selected: !this.value },
            ...this._automations.map((automation) => ({
                // Non-published automations can be selected (they may be published later),
                // but are marked so the author knows the step will fail until they are.
                name:
                    automation.status === "Published"
                        ? automation.name
                        : `${automation.name} (${automation.status.toLowerCase()})`,
                value: automation.id,
                selected: automation.id === this.value,
            })),
        ];

        // Keep a saved id that no longer resolves (deleted automation, or one outside the
        // current scope) visible instead of silently blanking the setting on save.
        if (this.value && !this._automations.some((automation) => automation.id === this.value)) {
            options.push({
                name: `Missing automation (${this.value})`,
                value: this.value,
                selected: true,
            });
        }

        return options;
    }

    #onChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        if (next === this.value) return;
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
                label="Automation"
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

export default UaAutomationPickerElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-picker": UaAutomationPickerElement;
    }
}
