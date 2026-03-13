import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { AutomationsService } from "../../api/sdk.gen.js";
import { UaRunTypeMapper } from "../../run/type-mapper.js";
import type { UaStatusCardData } from "./components/status-cards.element.js";
import type { UaActivityItem } from "./components/activity-list.element.js";
import type { UaRunItemModel } from "../../run/types.js";
import "./components/status-cards.element.js";
import "./components/activity-list.element.js";

@customElement("ua-automate-dashboard")
export class UaAutomateDashboardElement extends UmbLitElement {
    @state()
    private _cards: UaStatusCardData[] = [];

    @state()
    private _activity: UaActivityItem[] = [];

    @state()
    private _loading = true;

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();
    }

    async #loadData() {
        this._loading = true;

        // Load automations
        const { data: automationsData } = await tryExecute(
            this,
            AutomationsService.getAutomations({ query: { skip: 0, take: 500 } }),
        );

        if (!automationsData) {
            this._loading = false;
            return;
        }

        const published = automationsData.items.filter((a) => a.status === "Published").length;
        const draft = automationsData.items.filter((a) => a.status === "Draft").length;
        const inactive = automationsData.items.filter((a) => a.status === "Inactive").length;

        // Load recent runs from all automations
        const allRuns: (UaRunItemModel & { automationName: string })[] = [];
        const runPromises = automationsData.items.map(async (a) => {
            const { data } = await tryExecute(
                this,
                AutomationsService.getAutomationsByIdRuns({
                    path: { id: a.id },
                    query: { skip: 0, take: 10 },
                }),
            );
            if (data) {
                return data.items.map((r) => ({
                    ...UaRunTypeMapper.toItemModel(r),
                    automationName: a.name,
                }));
            }
            return [];
        });

        const results = await Promise.all(runPromises);
        for (const runs of results) {
            allRuns.push(...runs);
        }

        // Sort by started date descending
        allRuns.sort((a, b) => {
            const aTime = a.startedUtc ? new Date(a.startedUtc).getTime() : 0;
            const bTime = b.startedUtc ? new Date(b.startedUtc).getTime() : 0;
            return bTime - aTime;
        });

        const failedRuns = allRuns.filter((r) => r.status === "Failed").length;
        const runningRuns = allRuns.filter((r) => r.status === "Running" || r.status === "Suspended").length;

        this._cards = [
            { label: "Published", count: published, color: "positive", icon: "icon-check" },
            { label: "Draft", count: draft, color: "warning", icon: "icon-edit" },
            { label: "Inactive", count: inactive, color: "danger", icon: "icon-block" },
            { label: "Failed Runs", count: failedRuns, color: "danger", icon: "icon-alert" },
            { label: "In Progress", count: runningRuns, color: "warning", icon: "icon-nodes" },
        ];

        this._activity = allRuns.slice(0, 15).map((r) => ({
            runId: r.unique,
            automationName: r.automationName,
            status: r.status,
            startedUtc: r.startedUtc,
        }));

        this._loading = false;
    }

    override render() {
        if (this._loading) {
            return html`<div class="center"><uui-loader></uui-loader></div>`;
        }

        return html`
            <div class="uui-text">
                <ua-status-cards .cards=${this._cards}></ua-status-cards>

                <uui-box headline="Recent Activity" class="activity-box">
                    <ua-activity-list .items=${this._activity}></ua-activity-list>
                </uui-box>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                box-sizing: border-box;
                padding: var(--uui-size-layout-1);
                height: 100%;
                overflow-y: auto;
            }

            .center {
                display: flex;
                justify-content: center;
                align-items: center;
                padding: var(--uui-size-layout-3);
            }

            .header {
                display: flex;
                align-items: baseline;
                gap: var(--uui-size-space-3);
                margin-bottom: var(--uui-size-layout-1);
            }

            .subtitle {
                color: var(--uui-color-text-alt);
                font-size: var(--uui-size-5);
            }

            .activity-box {
                margin-top: var(--uui-size-layout-1);
            }
        `,
    ];
}

export default UaAutomateDashboardElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automate-dashboard": UaAutomateDashboardElement;
    }
}
