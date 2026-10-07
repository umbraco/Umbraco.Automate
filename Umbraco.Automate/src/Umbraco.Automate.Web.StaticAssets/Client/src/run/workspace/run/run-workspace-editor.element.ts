import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UA_RUN_WORKSPACE_CONTEXT } from "./run-workspace.context-token.js";
import { UA_RUN_WORKSPACE_ALIAS } from "../../constants.js";
import type { UaRunDetailModel } from "../../types.js";
import { getRunStatusColor } from "../../../core/index.js";

@customElement("ua-run-workspace-editor")
export class UaRunWorkspaceEditorElement extends UmbLitElement {
    @state()
    private _run?: UaRunDetailModel;

    constructor() {
        super();
        this.consumeContext(UA_RUN_WORKSPACE_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.run, (run) => {
                this._run = run;
            });
        });
    }

    override render() {
        if (!this._run) return html`<uui-loader></uui-loader>`;

        return html`
            <umb-workspace-editor alias="${UA_RUN_WORKSPACE_ALIAS}" .enforceNoFooter=${true}>
                <div id="header" slot="header">
                    <span class="title">Run ${this._run.unique.substring(0, 8)}...</span>
                    <uui-tag color=${getRunStatusColor(this._run.status)} look="secondary">
                        ${this._run.status}
                    </uui-tag>
                </div>
            </umb-workspace-editor>
        `;
    }

    static override styles = [
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

            .title {
                flex-grow: 1;
                font-size: var(--uui-size-6);
                font-weight: 600;
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

export default UaRunWorkspaceEditorElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-run-workspace-editor": UaRunWorkspaceEditorElement;
    }
}
