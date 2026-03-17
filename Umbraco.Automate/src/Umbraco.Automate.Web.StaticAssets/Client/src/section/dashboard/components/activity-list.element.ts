import { css, html, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { formatDateTime } from "../../../core/index.js";
import { UA_RUN_WORKSPACE_PATH } from "../../../run/workspace/run/paths.js";

export interface UaActivityItem {
    runId: string;
    automationName: string;
    status: string;
    startedUtc: string | null;
}

@customElement("ua-activity-list")
export class UaActivityListElement extends UmbLitElement {
    @state()
    items: UaActivityItem[] = [];

    #statusColor(status: string): string {
        switch (status) {
            case "Completed":
                return "positive";
            case "Running":
            case "Pending":
                return "warning";
            case "Failed":
                return "danger";
            default:
                return "default";
        }
    }

    override render() {
        if (this.items.length === 0) {
            return html`<p class="empty">${this.localize.term("uaRun_noRecentActivity")}</p>`;
        }

        return html`
            <div class="list">
                ${this.items.map(
                    (item) => html`
                        <a class="activity-item" href="${UA_RUN_WORKSPACE_PATH}/edit/${item.runId}">
                            <uui-tag color=${this.#statusColor(item.status)} look="secondary">
                                ${item.status}
                            </uui-tag>
                            <span class="activity-name">${item.automationName}</span>
                            <span class="activity-time">
                                ${item.startedUtc ? formatDateTime(item.startedUtc) : "-"}
                            </span>
                        </a>
                    `,
                )}
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            .list {
                display: flex;
                flex-direction: column;
            }

            .activity-item {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                padding: var(--uui-size-space-3) var(--uui-size-space-4);
                border-bottom: 1px solid var(--uui-color-border);
                text-decoration: none;
                color: inherit;
            }

            .activity-item:hover {
                background: var(--uui-color-surface-alt);
            }

            .activity-item:last-child {
                border-bottom: none;
            }

            .activity-name {
                flex: 1;
                font-weight: 500;
            }

            .activity-time {
                color: var(--uui-color-text-alt);
                font-size: var(--uui-size-4);
            }

            .empty {
                color: var(--uui-color-text-alt);
                text-align: center;
                padding: var(--uui-size-layout-2);
            }
        `,
    ];
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-activity-list": UaActivityListElement;
    }
}
