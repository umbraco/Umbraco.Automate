import { css, html, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbModalBaseElement } from "@umbraco-cms/backoffice/modal";
import { UmbValidationContext, umbBindToValidation } from "@umbraco-cms/backoffice/validation";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import type { UUIInputElement, UUIInputEvent, UUISelectElement, UUISelectEvent } from "@umbraco-cms/backoffice/external/uui";
import type { StepErrorBehaviorModel } from "../../../api/types.gen.js";
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
    private _name = "";

    @state()
    private _alias = "";

    @state()
    private _errorBehavior: StepErrorBehaviorModel = "Retry";

    @state()
    private _retryInterval = "";

    @state()
    private _maxRetries = "";

    @state()
    private _settings: Record<string, unknown> = {};

    @state()
    private _bindingSources: BindingSource[] = [];

    @state()
    private _connectionId: string | null = null;

    @state()
    private _availableConnections: UaConnectionItemModel[] = [];

    // Provides UMB_VALIDATION_CONTEXT to the descendant settings form and its umb-property
    // fields (the context API crosses shadow-DOM boundaries), so their mandatory validators
    // register here and #onSubmit can gate submission on validate().
    #validationContext = new UmbValidationContext(this);

    #catalogueRepo = new UaCatalogueRepository(this);
    #connectionRepo = new UaConnectionCollectionRepository(this);

    override connectedCallback() {
        super.connectedCallback();
        this._name = this.data?.name ?? "";
        this._alias = this.data?.alias ?? "";
        this._errorBehavior = this.data?.errorBehavior ?? "Retry";
        this._retryInterval = this.data?.retryInterval ?? "";
        this._maxRetries = this.data?.maxRetries != null ? String(this.data.maxRetries) : "";
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

    #onNameAliasChange(event: UmbChangeEvent) {
        const target = event.target as HTMLElement & { value?: string; alias: string };
        this._name = target.value ?? "";
        this._alias = target.alias;
    }

    #onErrorBehaviorChange(event: UUISelectEvent) {
        this._errorBehavior = (event.target as UUISelectElement).value as StepErrorBehaviorModel;
    }

    #onRetryIntervalChange(event: UUIInputEvent) {
        this._retryInterval = (event.composedPath()[0] as UUIInputElement).value.toString();
    }

    #onMaxRetriesChange(event: UUIInputEvent) {
        this._maxRetries = (event.composedPath()[0] as UUIInputElement).value.toString();
    }

    #onSettingsChange(event: CustomEvent<SettingsChangeDetail>) {
        this._settings = event.detail.settings;
    }

    #onConnectionChange(event: UUISelectEvent) {
        const next = (event.target as UUISelectElement).value as string;
        this._connectionId = next === "" ? null : next;
    }

    /**
     * Cross-references the alias against sibling steps in the automation — a check the alias
     * field's own validators can't express, since duplication isn't a property of this field's
     * value alone. Surfaced through the same validation-message channel as server errors — the
     * "server" type is required: UmbBindServerValidationToFormControl only renders that type
     * inline, filtering out "client" (reserved for the field's own local validators) — so it
     * renders under the field bound to "$.name" and blocks the shared validate() call.
     */
    #checkDuplicateAlias() {
        this.#validationContext.messages.removeMessagesByTypeAndPath("server", "$.name");

        const alias = this._alias.trim().toLowerCase();
        if (!alias) return;

        const isDuplicate = (this.data?.automationContext?.steps ?? []).some(
            (s) => s.id !== this.data?.stepId && s.alias?.toLowerCase() === alias,
        );
        if (isDuplicate) {
            this.#validationContext.messages.addMessage(
                "server",
                "$.name",
                this.localize.term("uaAutomation_stepAliasDuplicate"),
            );
        }
    }

    /**
     * Guards against maxRetries values that would otherwise be silently mangled: `Number(...)`
     * on invalid input (e.g. a stray "-") produces `NaN`, which serializes to `null` and quietly
     * changes the step's behaviour to "retry indefinitely" instead of respecting what was typed.
     * Blocks submission via the same "server"-type validation-message channel as the alias check.
     */
    #checkMaxRetries() {
        this.#validationContext.messages.removeMessagesByTypeAndPath("server", "$.maxRetries");

        const raw = this._maxRetries.trim();
        if (!raw) return;

        const parsed = Number.parseInt(raw, 10);
        if (!Number.isInteger(parsed) || parsed < 0 || String(parsed) !== raw) {
            this.#validationContext.messages.addMessage(
                "server",
                "$.maxRetries",
                this.localize.term("uaAutomation_stepMaxRetriesInvalid"),
            );
        }
    }

    async #onSubmit() {
        this.#checkDuplicateAlias();
        this.#checkMaxRetries();

        try {
            await this.#validationContext.validate();
        } catch {
            // Validation failed (e.g. a required field is empty) — keep the modal open
            // so the field-level errors stay visible.
            return;
        }
        this.value = {
            name: this._name.trim(),
            alias: this._alias.trim() || null,
            settings: this._settings,
            connectionId: this._connectionId,
            errorBehavior: this._errorBehavior,
            retryInterval: this._retryInterval.trim() || null,
            maxRetries: this._maxRetries.trim() ? Number.parseInt(this._maxRetries, 10) : null,
        };
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
                    ${this.#renderDetailsBox()}
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

    #renderDetailsBox() {
        const errorBehaviorOptions = (["Retry", "Suspend", "Terminate", "Compensate"] as const).map((value) => ({
            name: this.localize.term(`uaAutomation_stepErrorBehavior${value}`),
            value,
            selected: value === this._errorBehavior,
        }));

        return html`
            <uui-box>
                <umb-property-layout label=${this.localize.term("uaLabels_name")} orientation="vertical" mandatory>
                    <umb-input-with-alias
                        slot="editor"
                        .value=${this._name}
                        .alias=${this._alias}
                        placeholder=${this.localize.term("uaPlaceholders_enterName")}
                        alias-pattern="^[a-zA-Z][a-zA-Z0-9]*$"
                        ?auto-generate-alias=${this.data?.isNew}
                        required
                        @change=${this.#onNameAliasChange}
                        ${umbBindToValidation(this, "$.name", this._name)}
                    ></umb-input-with-alias>
                </umb-property-layout>
                <umb-property-layout
                    label=${this.localize.term("uaLabels_errorBehavior")}
                    description=${this.localize.term("uaAutomation_stepErrorBehaviorDescription")}
                    orientation="vertical"
                >
                    <uui-select
                        slot="editor"
                        .options=${errorBehaviorOptions}
                        @change=${this.#onErrorBehaviorChange}
                    ></uui-select>
                </umb-property-layout>
                ${this._errorBehavior === "Retry" ? this.#renderRetryFields() : nothing}
            </uui-box>
        `;
    }

    #renderRetryFields() {
        return html`
            <umb-property-layout
                label=${this.localize.term("uaLabels_retryInterval")}
                description=${this.localize.term("uaAutomation_stepRetryIntervalDescription")}
                orientation="vertical"
            >
                <uui-input
                    slot="editor"
                    .value=${this._retryInterval}
                    placeholder="00:00:30"
                    @input=${this.#onRetryIntervalChange}
                ></uui-input>
            </umb-property-layout>
            <umb-property-layout
                label=${this.localize.term("uaLabels_maxRetries")}
                description=${this.localize.term("uaAutomation_stepMaxRetriesDescription")}
                orientation="vertical"
            >
                <uui-input
                    slot="editor"
                    type="number"
                    min="0"
                    .value=${this._maxRetries}
                    @input=${this.#onMaxRetriesChange}
                    ${umbBindToValidation(this, "$.maxRetries", this._maxRetries)}
                ></uui-input>
            </umb-property-layout>
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
            /* Match ua-settings-form so the Details rows sit on the same rhythm as the
               settings fields below. */
            umb-property-layout {
                --uui-size-layout-1: var(--uui-size-space-2);
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
