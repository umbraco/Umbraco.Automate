import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaConnectionDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_CONNECTION_WORKSPACE_CONTEXT } from "../connection-workspace.context-token.js";

@customElement("ua-connection-info-workspace-view")
export class UaConnectionInfoWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaConnectionDetailModel;

    constructor() {
        super();
        this.consumeContext(UA_CONNECTION_WORKSPACE_CONTEXT, (context) => {
            if (context) {
                this.observe(context.data, (model) => {
                    this._model = model;
                });
            }
        });
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="container">
                <uui-box headline=${this.localize.term("uaLabels_history")}>
                    ${this._model.dateCreated
                        ? html`
                              <umb-property-layout label=${this.localize.term("uaLabels_created")} orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateCreated)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                    ${this._model.dateModified
                        ? html`
                              <umb-property-layout label=${this.localize.term("uaLabels_modified")} orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateModified)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                </uui-box>
            </div>

            <div class="container">
                <uui-box headline=${this.localize.term("general_general")}>
                    <umb-property-layout label=${this.localize.term("uaLabels_id")} orientation="vertical">
                        <div slot="editor">
                            ${this._model.unique === UA_EMPTY_GUID
                                ? html`<uui-tag color="default" look="placeholder">${this.localize.term("uaLabels_unsaved")}</uui-tag>`
                                : this._model.unique}
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label=${this.localize.term("uaLabels_alias")} orientation="vertical">
                        <div slot="editor">${this._model.alias || "-"}</div>
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

export default UaConnectionInfoWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-connection-info-workspace-view": UaConnectionInfoWorkspaceViewElement;
    }
}
