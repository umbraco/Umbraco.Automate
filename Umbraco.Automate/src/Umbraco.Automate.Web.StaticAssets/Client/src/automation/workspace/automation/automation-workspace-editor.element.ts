import { css, html, customElement, state, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UUIInputElement, UUIInputEvent } from "@umbraco-cms/backoffice/external/uui";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbBindToValidation, UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "./automation-workspace.context-token.js";
import { UA_AUTOMATION_WORKSPACE_ALIAS } from "../../constants.js";
import type { UaAutomationDetailModel } from "../../types.js";
import { UA_AUTOMATION_ROOT_WORKSPACE_PATH } from "../automation-root/paths.js";

@customElement("ua-automation-workspace-editor")
export class UaAutomationWorkspaceEditorElement extends UmbFormControlMixin(UmbLitElement) {
    #workspaceContext?: typeof UA_AUTOMATION_WORKSPACE_CONTEXT.TYPE;

    @state()
    private _model?: UaAutomationDetailModel;

    @state()
    private _isNew?: boolean;

    @state()
    private _aliasLocked = true;

    constructor() {
        super();

        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.#workspaceContext = context;
            this.observe(context.data, (model) => {
                this._model = model;
            });
            this.observe(context.isNew, (isNew) => {
                this._isNew = isNew;
                if (isNew) {
                    requestAnimationFrame(() => {
                        (this.shadowRoot?.querySelector("#name") as HTMLElement)?.focus();
                    });
                }
            });
        });
    }

    protected override firstUpdated(_changedProperties: any) {
        super.firstUpdated(_changedProperties);
        const nameInput = this.shadowRoot?.querySelector<UUIInputElement>("#name");
        if (nameInput) this.addFormControlElement(nameInput as any);
    }

    #onNameChange(event: UUIInputEvent) {
        event.stopPropagation();
        const target = event.composedPath()[0] as UUIInputElement;
        const name = target.value.toString();
        this.#workspaceContext?.updateProperty("name", name);

        if (this._aliasLocked && this._isNew) {
            this.#workspaceContext?.updateProperty("alias", this.#generateAlias(name));
        }
    }

    #onAliasChange(event: UUIInputEvent) {
        event.stopPropagation();
        const target = event.composedPath()[0] as UUIInputElement;
        this.#workspaceContext?.updateProperty("alias", target.value.toString());
    }

    #onToggleAliasLock() {
        this._aliasLocked = !this._aliasLocked;
    }

    #generateAlias(name: string): string {
        return name
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, "-")
            .replace(/^-|-$/g, "");
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        return html`
            <umb-workspace-editor alias="${UA_AUTOMATION_WORKSPACE_ALIAS}">
                <div id="header" slot="header">
                    <uui-button href=${UA_AUTOMATION_ROOT_WORKSPACE_PATH} label="Back to automations" compact>
                        <uui-icon name="icon-arrow-left"></uui-icon>
                    </uui-button>
                    <uui-input
                        id="name"
                        .value=${this._model.name}
                        @input="${this.#onNameChange}"
                        label="Name"
                        placeholder=${this.localize.term("uaPlaceholders_enterName")}
                        required
                        maxlength="255"
                        ${umbBindToValidation(this, "$.name", this._model.name)}
                    >
                        <uui-input-lock
                            slot="append"
                            id="alias"
                            name="alias"
                            label="Alias"
                            placeholder=${this.localize.term("uaPlaceholders_enterAlias")}
                            .value=${this._model.alias}
                            ?auto-width=${!!this._model.name}
                            ?locked=${this._aliasLocked}
                            ?readonly=${this._aliasLocked || !this._isNew}
                            @input=${this.#onAliasChange}
                            @lock-change=${this.#onToggleAliasLock}
                            required
                            maxlength="100"
                            pattern="^[a-z0-9\\-]+$"
                            ${umbBindToValidation(this, "$.alias", this._model.alias)}
                        ></uui-input-lock>
                    </uui-input>
                </div>

                ${when(
                    !this._isNew && this._model,
                    () =>
                        html`<umb-workspace-entity-action-menu slot="action-menu"></umb-workspace-entity-action-menu>`,
                )}
            </umb-workspace-editor>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                width: 100%;
                height: 100%;
            }

            #header {
                display: flex;
                flex: 1 1 auto;
                gap: var(--uui-size-space-3);
                align-items: center;
            }

            #name {
                width: 100%;
                flex: 1 1 auto;
                align-items: center;
            }

            #footer {
                padding: 0 var(--uui-size-layout-1);
            }

            uui-loader {
                display: block;
                margin: auto;
                position: absolute;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
            }
        `,
    ];
}

export default UaAutomationWorkspaceEditorElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-workspace-editor": UaAutomationWorkspaceEditorElement;
    }
}
