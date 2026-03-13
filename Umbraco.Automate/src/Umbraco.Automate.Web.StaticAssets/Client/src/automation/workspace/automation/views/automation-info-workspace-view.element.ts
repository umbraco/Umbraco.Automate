import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { UaAutomationDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID, formatDateTime } from "../../../../core/index.js";
import { UA_AUTOMATION_WORKSPACE_CONTEXT } from "../automation-workspace.context-token.js";

@customElement("ua-automation-info-workspace-view")
export class UaAutomationInfoWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaAutomationDetailModel;

    constructor() {
        super();
        this.consumeContext(UA_AUTOMATION_WORKSPACE_CONTEXT, (context) => {
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
                <uui-box headline="Status">
                    <umb-property-layout label="Status" orientation="vertical">
                        <div slot="editor">
                            <uui-tag color=${this.#statusColor(this._model.status)} look="secondary">
                                ${this._model.status}
                            </uui-tag>
                        </div>
                    </umb-property-layout>
                    <umb-property-layout label="Enabled" orientation="vertical">
                        <div slot="editor">
                            <uui-tag color=${this._model.isEnabled ? "positive" : "default"} look="secondary">
                                ${this._model.isEnabled ? "Enabled" : "Disabled"}
                            </uui-tag>
                        </div>
                    </umb-property-layout>
                </uui-box>

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
            </div>

            <div class="container">
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
                    <umb-property-layout label="Draft Version" orientation="vertical">
                        <div slot="editor">${this._model.draftVersion}</div>
                    </umb-property-layout>
                    <umb-property-layout label="Published Version" orientation="vertical">
                        <div slot="editor">${this._model.publishedVersion ?? "-"}</div>
                    </umb-property-layout>
                </uui-box>
            </div>
        `;
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
