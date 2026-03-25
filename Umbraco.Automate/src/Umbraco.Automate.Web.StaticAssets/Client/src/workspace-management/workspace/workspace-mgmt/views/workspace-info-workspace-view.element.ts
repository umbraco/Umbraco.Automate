import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbBindToValidation } from "@umbraco-cms/backoffice/validation";
import type { UaWorkspaceDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT } from "../workspace-mgmt-workspace.context-token.js";

import "../../../../connection/components/input-connection/input-connection.element.js";

@customElement("ua-workspace-info-workspace-view")
export class UaWorkspaceInfoWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaWorkspaceDetailModel;

    #workspaceContext?: typeof UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT.TYPE;

    constructor() {
        super();
        this.consumeContext(UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT, (context) => {
            if (context) {
                this.#workspaceContext = context;
                this.observe(context.data, (model) => {
                    this._model = model;
                });
            }
        });
    }

    #onServiceAccountChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLInputElement & { selection: string[] };
        const selected = target.selection?.[0] ?? UA_EMPTY_GUID;
        this.#workspaceContext?.updateProperty("serviceAccountKey", selected);
    }

    #onUserGroupsChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLElement & { selection: string[] };
        this.#workspaceContext?.updateProperty("userGroups", [...target.selection]);
    }

    #onAllowedConnectionsChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLElement & { selection: string[] };
        this.#workspaceContext?.updateProperty("allowedConnections", [...target.selection]);
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="container">
                <uui-box headline="Membership">
                    <umb-property-layout
                        label=${this.localize.term("uaWorkspace_serviceAccountKey")}
                        description="The API user identity used when running automations in this workspace."
                        orientation="vertical"
                        mandatory
                    >
                        <umb-user-input
                            slot="editor"
                            max="1"
                            required
                            .selection=${this._model.serviceAccountKey && this._model.serviceAccountKey !== UA_EMPTY_GUID
                                ? [this._model.serviceAccountKey]
                                : []}
                            @change=${this.#onServiceAccountChange}
                            ${umbBindToValidation(this, "$.serviceAccountKey", this._model.serviceAccountKey)}
                        ></umb-user-input>
                    </umb-property-layout>

                    <umb-property-layout
                        label=${this.localize.term("uaWorkspace_userGroups")}
                        description="User groups that have access to automations in this workspace."
                        orientation="vertical"
                        mandatory
                    >
                        <umb-user-group-input
                            slot="editor"
                            required
                            .selection=${this._model.userGroups}
                            @change=${this.#onUserGroupsChange}
                            ${umbBindToValidation(this, "$.userGroups", this._model.userGroups)}
                        ></umb-user-group-input>
                    </umb-property-layout>

                    <umb-property-layout
                        label=${this.localize.term("uaWorkspace_allowedConnections")}
                        description="Connections that automations in this workspace are allowed to use."
                        orientation="vertical"
                    >
                        <ua-input-connection
                            slot="editor"
                            .selection=${this._model.allowedConnections}
                            @change=${this.#onAllowedConnectionsChange}
                        ></ua-input-connection>
                    </umb-property-layout>
                </uui-box>
            </div>

            <div class="container">
                <uui-box headline="History">
                    ${this._model.dateCreated
                        ? html`
                              <umb-property-layout label="Created" orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateCreated)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                    ${this._model.dateModified
                        ? html`
                              <umb-property-layout label="Modified" orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateModified)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                </uui-box>

                <uui-box headline=${this.localize.term("general_general")}>
                    <umb-property-layout label="Id" orientation="vertical">
                        <div slot="editor">
                            ${this._model.unique === UA_EMPTY_GUID
                                ? html`<uui-tag color="default" look="placeholder">Unsaved</uui-tag>`
                                : this._model.unique}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Alias" orientation="vertical">
                        <div slot="editor">${this._model.alias || "-"}</div>
                    </umb-property-layout>
                    <umb-property-layout label="Version" orientation="vertical">
                        <div slot="editor">${this._model.version}</div>
                    </umb-property-layout>
                </uui-box>
            </div>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            :host {
                display: grid;
                gap: var(--uui-size-layout-1);
                padding: var(--uui-size-layout-1);
                grid-template-columns: 1fr 350px;
            }

            .container {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-layout-1);
            }

            uui-box {
                --uui-box-default-padding: 0 var(--uui-size-space-5);
            }

            umb-property-layout[orientation="vertical"]:not(:last-child) {
                padding-bottom: 0;
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

export default UaWorkspaceInfoWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-info-workspace-view": UaWorkspaceInfoWorkspaceViewElement;
    }
}
