import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadStructureForEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import type { UaWorkspaceDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_WORKSPACE_MGMT_ENTITY_TYPE } from "../../../entity.js";
import { UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT } from "../workspace-mgmt-workspace.context-token.js";

import "../../../../core/version-history/components/version-history/version-history.element.js";

@customElement("ua-workspace-info-workspace-view")
export class UaWorkspaceInfoWorkspaceViewElement extends UmbLitElement {
    #workspaceContext?: typeof UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT.TYPE;
    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

    @state()
    private _model?: UaWorkspaceDetailModel;

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
        this.consumeContext(UMB_ACTION_EVENT_CONTEXT, (context) => {
            this.#eventContext = context;
        });
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        return html`
            <div class="container">
                <ua-version-history
                    entity-id=${this._model.unique}
                    entity-type="workspace"
                    .currentVersion=${this._model.version}
                    @rollback=${this.#onRollback}
                >
                </ua-version-history>
            </div>

            <div class="container">
                <uui-box headline=${this.localize.term("uaLabels_info")}>
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
                    ${this._model.dateCreated
                        ? html`
                              <umb-property-layout label=${this.localize.term("uaLabels_dateCreated")} orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateCreated)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                    ${this._model.dateModified
                        ? html`
                              <umb-property-layout label=${this.localize.term("uaLabels_dateModified")} orientation="vertical">
                                  <div slot="editor">${formatDateTime(this._model.dateModified)}</div>
                              </umb-property-layout>
                          `
                        : ""}
                </uui-box>
            </div>
        `;
    }

    async #onRollback() {
        const unique = this._model?.unique;
        if (unique && unique !== UA_EMPTY_GUID) {
            await this.#workspaceContext?.reload();
            this.#eventContext?.dispatchEvent(
                new UmbRequestReloadStructureForEntityEvent({
                    entityType: UA_WORKSPACE_MGMT_ENTITY_TYPE,
                    unique,
                }),
            );
        }
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
