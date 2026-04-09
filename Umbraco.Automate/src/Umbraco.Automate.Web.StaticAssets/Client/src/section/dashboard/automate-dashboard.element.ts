import { css, customElement, html, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UaAutomationCollectionServerDataSource } from "../../automation/repository/collection/automation-collection.server.data-source.js";
import { UaRunCollectionServerDataSource } from "../../run/repository/collection/run-collection.server.data-source.js";
import { UaWorkspaceCollectionServerDataSource } from "../../workspace-management/repository/collection/workspace-collection.server.data-source.js";
import { UA_CREATE_WORKSPACE_MGMT_WORKSPACE_PATH_PATTERN } from "../../workspace-management/workspace/workspace-mgmt/paths.js";
import type { UaStatusCardData } from "./components/status-cards.element.js";
import type { UaActivityItem } from "./components/activity-list.element.js";
import type { UaRunItemModel } from "../../run/types.js";
import "./components/status-cards.element.js";
import "./components/activity-list.element.js";

type WelcomeState = "none" | "no-workspaces" | "no-automations";

@customElement("ua-automate-dashboard")
export class UaAutomateDashboardElement extends UmbLitElement {
    #automationSource = new UaAutomationCollectionServerDataSource(this);
    #runSource = new UaRunCollectionServerDataSource(this);
    #workspaceSource = new UaWorkspaceCollectionServerDataSource(this);

    @state()
    private _cards: UaStatusCardData[] = [];

    @state()
    private _activity: UaActivityItem[] = [];

    @state()
    private _loading = true;

    @state()
    private _welcomeState: WelcomeState = "none";

    override connectedCallback() {
        super.connectedCallback();
        this.#loadData();
    }

    async #loadData() {
        this._loading = true;

        // Load automations
        const { data: automationsData } = await this.#automationSource.getCollection({ skip: 0, take: 500 });

        if (!automationsData) {
            this._loading = false;
            return;
        }

        // Show welcome state when there are no automations
        if (automationsData.total === 0) {
            const { data: workspacesData } = await this.#workspaceSource.getCollection({ skip: 0, take: 1 });
            this._welcomeState = (workspacesData?.total ?? 0) > 0 ? "no-automations" : "no-workspaces";
            this._loading = false;
            return;
        }

        const published = automationsData.items.filter((a) => a.status === "Published").length;
        const draft = automationsData.items.filter((a) => a.status === "Draft").length;
        const inactive = automationsData.items.filter((a) => a.status === "Inactive").length;

        // Load recent runs from all automations
        const allRuns: (UaRunItemModel & { automationName: string })[] = [];
        const runPromises = automationsData.items.map(async (a) => {
            const { data } = await this.#runSource.getCollection({ automationId: a.unique, skip: 0, take: 10 });
            if (data) {
                return data.items.map((r) => ({
                    ...r,
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

        if (this._welcomeState !== "none") {
            return this.#renderWelcome();
        }

        return html`
            <div class="uui-text">
                <ua-status-cards .cards=${this._cards}></ua-status-cards>

                <uui-box headline=${this.localize.term("uaDashboard_recentActivity")} class="activity-box">
                    <ua-activity-list .items=${this._activity}></ua-activity-list>
                </uui-box>
            </div>
        `;
    }

    #renderWelcome() {
        const hasWorkspaces = this._welcomeState === "no-automations";

        return html`
            <div class="uui-text">
                <div class="welcome">
                    <uui-icon name="icon-mindmap" class="welcome-icon"></uui-icon>
                    <h1 class="uui-h2" style="margin-top: 0;">
                        ${this.localize.term("uaDashboard_welcomeHeadline")}
                    </h1>
                    <p class="welcome-intro">
                        ${this.localize.term("uaDashboard_welcomeIntro")}
                    </p>
                    <p>${hasWorkspaces
                        ? this.localize.term("uaDashboard_welcomeBodyHasWorkspaces")
                        : this.localize.term("uaDashboard_welcomeBody")}</p>
                    ${hasWorkspaces
                        ? html``
                        : html`<uui-button
                              look="primary"
                              color="positive"
                              label=${this.localize.term("uaDashboard_welcomeCta")}
                              href=${UA_CREATE_WORKSPACE_MGMT_WORKSPACE_PATH_PATTERN.generateAbsolute({})}>
                              ${this.localize.term("uaDashboard_welcomeCta")}
                          </uui-button>`}
                </div>
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

            .welcome {
                max-width: 600px;
                margin: 0 auto;
                text-align: center;
                padding-top: var(--uui-size-layout-2);
            }

            .welcome-icon {
                font-size: 80px;
                color: var(--uui-color-interactive);
                margin-bottom: var(--uui-size-space-4);
            }

            .uui-text .welcome-intro {
                font-size: var(--uui-size-6);
                color: var(--uui-color-text-alt);
                font-weight: 400;
                margin-bottom: var(--uui-size-layout-2);
            }

            .welcome uui-button {
                margin-top: var(--uui-size-space-5);
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
