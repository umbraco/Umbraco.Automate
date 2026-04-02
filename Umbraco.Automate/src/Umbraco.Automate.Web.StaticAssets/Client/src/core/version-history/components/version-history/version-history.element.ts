import { css, customElement, html, nothing, property, repeat, state, when } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_MODAL_MANAGER_CONTEXT } from "@umbraco-cms/backoffice/modal";
import type { UaVersionHistoryItem } from "../../types.js";
import { UA_ROLLBACK_MODAL } from "../../modals/rollback-modal/rollback-modal.token.js";
import { UaVersionHistoryRepository } from "../../repository/version-history.repository.js";
import { UmbUserItemModel, UmbUserItemRepository } from "@umbraco-cms/backoffice/user";
import { formatDateTime } from "../../../utils/index.js";
import { UA_EMPTY_GUID } from "../../../index.js";
import { UUIPaginationEvent } from "@umbraco-cms/backoffice/external/uui";

const PAGE_SIZE = 10;

/**
 * A reusable version history component that displays version history
 * for automations. Pass the entity ID as an attribute.
 *
 * @element ua-version-history
 * @fires rollback - Fired after a successful rollback so parent can reload entity data
 */
@customElement("ua-version-history")
export class UaVersionHistoryElement extends UmbLitElement {
    #versionRepository = new UaVersionHistoryRepository(this);
    #userItemRepository = new UmbUserItemRepository(this);

    #modalManager?: typeof UMB_MODAL_MANAGER_CONTEXT.TYPE;

    #userMap = new Map<string, UmbUserItemModel>();

    /**
     * The unique identifier of the automation.
     */
    @property({ type: String, attribute: "entity-id" })
    entityId?: string;

    /**
     * The current version of the automation.
     * When this changes, the version history is refreshed.
     */
    @property({ attribute: false })
    currentVersion?: number;

    /**
     * The published version of the automation, if applicable.
     */
    @property({ attribute: false })
    publishedVersion?: number | null;

    @state()
    private _currentVersion?: number;

    @state()
    private _publishedVersion?: number | null;

    @state()
    private _versions: UaVersionHistoryItem[] = [];

    @state()
    private _totalVersions = 0;

    @state()
    private _loading = false;

    @state()
    private _currentPage = 1;

    constructor() {
        super();

        this.consumeContext(UMB_MODAL_MANAGER_CONTEXT, (context) => {
            this.#modalManager = context;
        });
    }

    override connectedCallback() {
        super.connectedCallback();
        if (this.entityId) {
            this.#loadVersionHistory();
        }
    }

    override updated(changedProperties: Map<string, unknown>) {
        super.updated(changedProperties);
        if (changedProperties.has("entityId")) {
            if (this.entityId) {
                this._currentPage = 1;
                this.#loadVersionHistory();
            }
        }
        // Refresh when currentVersion changes (but not on initial undefined->value)
        if (changedProperties.has("currentVersion") && changedProperties.get("currentVersion") !== undefined) {
            this.#loadVersionHistory();
        }
    }

    /**
     * Refreshes the version history data.
     * Call this after entity data has been modified.
     */
    public refresh(): void {
        this.#loadVersionHistory();
    }

    async #loadVersionHistory() {
        if (!this.entityId || this.entityId === UA_EMPTY_GUID) return;

        this._loading = true;
        try {
            const skip = (this._currentPage - 1) * PAGE_SIZE;
            const { data: response } = await this.#versionRepository.getVersionHistory(this.entityId, skip, PAGE_SIZE);
            if (response) {
                this._versions = response.versions;
                this._totalVersions = response.totalVersions;
                this._currentVersion = response.currentVersion;
                this._publishedVersion = response.publishedVersion;
                this.#requestAndCacheUserItems();
            }
        } finally {
            this._loading = false;
        }
    }

    async #requestAndCacheUserItems() {
        const allUsers = this._versions?.map((item) => item.createdByUserId?.toString()).filter(Boolean) as string[];
        const uniqueUsers = [...new Set(allUsers)];
        const uncachedUsers = uniqueUsers.filter((unique) => !this.#userMap.has(unique));

        if (uncachedUsers.length === 0) return;

        const { data: items } = await this.#userItemRepository.requestItems(uncachedUsers);

        if (items) {
            items.forEach((item) => {
                this.#userMap.set(item.unique, item);
                this.requestUpdate("_versions");
            });
        }
    }

    get #totalPages(): number {
        return Math.ceil(this._totalVersions / PAGE_SIZE);
    }

    async #onCompareClick(version: number) {
        if (!this.entityId || !this.#modalManager || this._currentVersion === undefined) return;

        const { data: comparison } = await this.#versionRepository.compareVersions(
            this.entityId,
            version,
            this._currentVersion,
        );
        if (!comparison) return;

        const modalContext = this.#modalManager.open(this, UA_ROLLBACK_MODAL, {
            data: {
                fromVersion: version,
                toVersion: this._currentVersion,
                changes: comparison.changes,
            },
        });

        const result = await modalContext.onSubmit().catch(() => undefined);
        if (result?.rollback) {
            const { error } = await this.#versionRepository.rollback(this.entityId, version);
            if (!error) {
                this.dispatchEvent(new CustomEvent("rollback", { bubbles: true, composed: true }));
                await this.#loadVersionHistory();
            }
        }
    }

    #onPageChange(event: UUIPaginationEvent) {
        this._currentPage = event.target.current;
        this.#loadVersionHistory();
    }

    override render() {
        if (!this.entityId) {
            return nothing;
        }

        return html`
            <uui-box headline=${this.localize.term("uaVersionHistory_history")}>
                ${when(
                    this._loading,
                    () => html`
                        <div class="loading">
                            <uui-loader></uui-loader>
                        </div>
                    `,
                    () => this.#renderContent(),
                )}
            </uui-box>
        `;
    }

    #renderContent() {
        if (this._versions.length === 0) {
            return html`<p class="no-versions">${this.localize.term("uaVersionHistory_noVersionsYet")}</p>`;
        }

        return html`
            <div class="versions-list">
                ${repeat(
                    this._versions,
                    (v) => v.id,
                    (v) => this.#renderItem(v),
                )}
            </div>
            ${this.#totalPages > 1 ? this.#renderPagination() : nothing}
        `;
    }

    #renderItem(version: UaVersionHistoryItem) {
        const isCurrent = version.version === this._currentVersion;
        const isPublished = version.version === this._publishedVersion;
        const user = this.#userMap.get(version.createdByUserId?.toString() || "");
        return html`
            <div class="version-item">
                <div class="user-info">
                    <umb-user-avatar
                        .name=${user?.name}
                        .kind=${user?.kind}
                        .imgUrls=${user?.avatarUrls ?? []}
                    >
                    </umb-user-avatar>
                    <div>
                        <span class="name">${user?.name}</span>
                        <span class="detail">${formatDateTime(version.dateCreated)}</span>
                    </div>
                </div>
                <div class="tags">
                    ${isCurrent
                        ? html`<uui-tag look="primary">${this.localize.term("uaLabels_current")}</uui-tag>`
                        : html`<uui-tag look="secondary">v${version.version}</uui-tag>`}
                    ${isPublished ? html`<uui-tag look="primary" color="positive">${this.localize.term("uaLabels_published")}</uui-tag>` : nothing}
                </div>
                ${!isCurrent
                    ? html`
                          <div class="actions">
                              <uui-button
                                  look="secondary"
                                  label=${this.localize.term("uaVersionHistory_compare")}
                                  @click=${() => this.#onCompareClick(version.version)}
                              >
                                  ${this.localize.term("uaVersionHistory_compare")}
                              </uui-button>
                          </div>
                      `
                    : nothing}
            </div>
        `;
    }

    #renderPagination() {
        return html`
            <div class="pagination-wrapper">
                <uui-pagination
                    .current=${this._currentPage}
                    .total=${this.#totalPages}
                    firstlabel=${this.localize.term("general_first")}
                    previouslabel=${this.localize.term("general_previous")}
                    nextlabel=${this.localize.term("general_next")}
                    lastlabel=${this.localize.term("general_last")}
                    @change=${this.#onPageChange}
                ></uui-pagination>
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
            }

            .loading {
                display: flex;
                justify-content: center;
                padding: var(--uui-size-space-5);
            }

            .no-versions {
                text-align: center;
                color: var(--uui-color-text-alt);
                padding: var(--uui-size-space-5);
                margin: 0;
            }

            .versions-list {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-4);
            }

            .version-item {
                display: flex;
                gap: var(--uui-size-space-5);
                align-items: center;
                border-bottom: 1px solid var(--uui-color-border);
                padding-bottom: var(--uui-size-space-4);
            }

            .version-item:last-child {
                border-bottom: none;
            }

            .tags {
                display: flex;
                gap: var(--uui-size-space-2);
            }

            .actions {
                flex: 1;
                display: flex;
                justify-content: flex-end;
            }

            .user-info {
                position: relative;
                display: flex;
                align-items: flex-end;
                gap: var(--uui-size-space-4);
            }

            .user-info div {
                display: flex;
                flex-direction: column;
                min-width: var(--uui-size-60);
            }

            .detail {
                font-size: var(--uui-size-4);
                color: var(--uui-color-text-alt);
                line-height: 1;
            }

            .pagination-wrapper {
                display: block;
                padding-top: var(--uui-size-space-5);
                border-top: 1px solid var(--uui-color-border);
            }
        `,
    ];
}

export default UaVersionHistoryElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-version-history": UaVersionHistoryElement;
    }
}
