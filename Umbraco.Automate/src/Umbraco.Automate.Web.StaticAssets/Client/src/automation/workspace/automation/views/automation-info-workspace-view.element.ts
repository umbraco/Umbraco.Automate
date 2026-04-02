import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_ACTION_EVENT_CONTEXT } from "@umbraco-cms/backoffice/action";
import { UmbRequestReloadStructureForEntityEvent } from "@umbraco-cms/backoffice/entity-action";
import type { UaAutomationDetailModel } from "../../../types.js";
import { UA_AUTOMATION_ENTITY_TYPE } from "../../../constants.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

import "../../../../core/version-history/components/version-history/version-history.element.js";

@customElement("ua-automation-info-workspace-view")
export class UaAutomationInfoWorkspaceViewElement extends UmbLitElement {
    #workspaceContext?: typeof UA_AUTOMATION_WORKSPACE_CONTEXT.TYPE;
    #eventContext?: typeof UMB_ACTION_EVENT_CONTEXT.TYPE;

    @state()
    private _model?: UaAutomationDetailModel;

    constructor() {
        super();
        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
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
                    .currentVersion=${this._model.draftVersion}
                    .publishedVersion=${this._model.publishedVersion}
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
                    <umb-property-layout label=${this.localize.term("uaLabels_status")} orientation="vertical">
                        <div slot="editor">
                            <uui-tag color=${this.#statusColor(this._model.status)} look="secondary">
                                ${this._model.status}
                            </uui-tag>
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label=${this.localize.term("uaLabels_enabled")} orientation="vertical">
                        <div slot="editor">
                            <uui-tag color=${this._model.isEnabled ? "positive" : "default"} look="secondary">
                                ${this._model.isEnabled ? this.localize.term("uaLabels_enabled") : this.localize.term("uaLabels_disabled")}
                            </uui-tag>
                        </div>
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
        const unique = this.#workspaceContext?.getUnique();
        if (unique) {
            await this.#workspaceContext?.load(unique);
            this.#eventContext?.dispatchEvent(
                new UmbRequestReloadStructureForEntityEvent({
                    entityType: UA_AUTOMATION_ENTITY_TYPE,
                    unique,
                }),
            );
        }
    }

    #statusColor(status: string): string {
        switch (status) {
            case "Published":
                return "positive";
            case "Draft":
                return "warning";
            case "Inactive":
                return "danger";
            default:
                return "default";
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

export default UaAutomationInfoWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-info-workspace-view": UaAutomationInfoWorkspaceViewElement;
    }
}
