import { css, html, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import type { UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import type { UaNodeSettingsModalData, UaNodeSettingsModalValue } from "./types.js";
import type { SettingsChangeDetail } from "../../../core/components/settings-form/settings-form.element.js";
import type { BindingSource } from "../../../core/utils/binding-context.utils.js";
import { buildBindingSources } from "../../../core/utils/binding-context.utils.js";
import { UaCatalogueRepository } from "../../../catalogue/repository/catalogue.repository.js";
import { UaConnectionCollectionRepository } from "../../../connection/repository/collection/connection-collection.repository.js";
import type { UaConnectionItemModel } from "../../../connection/types.js";
import { WorkspacesService } from "../../../api/sdk.gen.js";
import "../../../core/components/settings-form/settings-form.element.js";

@customElement("ua-node-settings-modal")
export class UaNodeSettingsModalElement extends UmbModalBaseElement<
    UaNodeSettingsModalData,
    UaNodeSettingsModalValue
> {
    @state()
    private _settings: Record<string, unknown> = {};

    @state()
    private _bindingSources: BindingSource[] = [];

    @state()
    private _connectionId: string | null = null;

    @state()
    private _availableConnections: UaConnectionItemModel[] = [];

    #catalogueRepo = new UaCatalogueRepository(this);
    #connectionRepo = new UaConnectionCollectionRepository(this);

    override connectedCallback() {
        super.connectedCallback();
        this._settings = { ...this.data?.settings };
        this._connectionId = this.data?.connectionId ?? null;
        this.#loadBindingSources();
        this.#loadConnectionOptions();
    }

    async #loadBindingSources() {
        const ctx = this.data?.automationContext;
        if (!ctx || !this.data?.stepId) return;

        this._bindingSources = await buildBindingSources(
            this.data.stepId,
            ctx.trigger ?? null,
            ctx.steps,
            ctx.connections,
            this.#catalogueRepo,
        );
    }

    /**
     * Populates the per-step connection picker with the workspace's allowed connections
     * that match the action's required connection type. The picker is only surfaced when
     * the user has a real choice to make — an action that doesn't need a connection, or a
     * workspace that has one or zero matching connections, auto-resolves at runtime.
     */
    async #loadConnectionOptions() {
        if (!this.data?.workspaceId || !this.data?.actionAlias) return;

        const { data: actions } = await this.#catalogueRepo.requestActions();
        const action = actions?.find((a) => a.alias === this.data!.actionAlias);
        const connectionTypeAlias = action?.connectionTypeAlias ?? null;
        if (!connectionTypeAlias) return;

        const { data: workspace } = await WorkspacesService.getWorkspacesById({
            path: { id: this.data.workspaceId },
        });
        const allowed = new Set(workspace?.allowedConnections ?? []);
        if (allowed.size === 0) return;

        const { data: collection } = await this.#connectionRepo.requestCollection({
            skip: 0,
            take: 1000,
        });
        const items = (collection?.items ?? []) as UaConnectionItemModel[];

        this._availableConnections = items.filter(
            (item) => item.type === connectionTypeAlias && allowed.has(item.unique),
        );
    }

    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        this._settings = event.detail.settings;
    }

    #onConnectionChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        this._connectionId = next === "" ? null : next;
    }

    #onSubmit() {
        this.value = { settings: this._settings, connectionId: this._connectionId };
        this.modalContext?.submit();
    }

    #onCancel() {
        this.modalContext?.reject();
    }

    override render() {
        if (!this.data) return html``;

        // Only surface the connection picker when the workspace has a real choice to offer.
        const hasConnectionChoice = this._availableConnections.length >= 2;

        return html`
            <umb-body-layout .headline=${this.data.actionName}>
                <div id="content">
                    ${hasConnectionChoice ? this.#renderConnectionBox() : nothing}
                    ${hasConnectionChoice
                        ? html`<uui-box headline=${this.localize.term("uaLabels_settings")}>
                              ${this.#renderSettingsForm(true)}
                          </uui-box>`
                        : this.#renderSettingsForm(false)}
                </div>
                <div slot="actions">
                    <uui-button
                        label=${this.localize.term("uaGeneral_close")}
                        @click=${this.#onCancel}
                    ></uui-button>
                    <uui-button
                        look="primary"
                        color="positive"
                        label=${this.localize.term("uaGeneral_save")}
                        @click=${this.#onSubmit}
                    ></uui-button>
                </div>
            </umb-body-layout>
        `;
    }

    // `noBox` avoids nesting uui-boxes when the settings form is already inside one;
    // ua-settings-form otherwise wraps each group in its own uui-box.
    #renderSettingsForm(noBox: boolean) {
        return html`
            <ua-settings-form
                label-on-top
                ?no-box=${noBox}
                .fields=${this.data!.schema.fields}
                .values=${this._settings}
                .bindingSources=${this._bindingSources}
                @ua:settings-change=${this.#onSettingsChange}
            ></ua-settings-form>
        `;
    }

    #renderConnectionBox() {
        const options = [
            {
                name: this.localize.term("uaAutomation_stepConnectionAutoResolve"),
                value: "",
                selected: this._connectionId === null,
            },
            ...this._availableConnections.map((c) => ({
                name: c.name,
                value: c.unique,
                selected: c.unique === this._connectionId,
            })),
        ];

        return html`
            <uui-box headline=${this.localize.term("uaLabels_connection")}>
                <p class="description">
                    ${this.localize.term("uaAutomation_stepConnectionDescription")}
                </p>
                <uui-select
                    .options=${options}
                    @change=${this.#onConnectionChange}
                ></uui-select>
            </uui-box>
        `;
    }

    static override styles = [
        css`
            #content {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }
            .description {
                margin: 0 0 var(--uui-size-space-4) 0;
                color: var(--uui-color-text-alt);
            }
            uui-select {
                width: 100%;
            }
        `,
    ];
}

export default UaNodeSettingsModalElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-node-settings-modal": UaNodeSettingsModalElement;
    }
}
