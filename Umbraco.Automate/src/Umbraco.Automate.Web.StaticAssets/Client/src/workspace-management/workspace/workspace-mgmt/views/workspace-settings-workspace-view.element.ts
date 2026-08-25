import { css, html, nothing, customElement, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { umbBindToValidation } from "@umbraco-cms/backoffice/validation";
import { UmbUserItemRepository } from "@umbraco-cms/backoffice/user";
import { UmbUserGroupItemRepository } from "@umbraco-cms/backoffice/user-group";
import type { UaWorkspaceDetailModel } from "../../../types.js";
import { UA_EMPTY_GUID } from "../../../../core/index.js";
import { UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT } from "../workspace-mgmt-workspace.context-token.js";

import "../../../../connection/components/input-connection/input-connection.element.js";

@customElement("ua-workspace-settings-workspace-view")
export class UaWorkspaceSettingsWorkspaceViewElement extends UmbLitElement {
    @state()
    private _model?: UaWorkspaceDetailModel;

    // True when the workspace's serviceAccountKey doesn't resolve to a real user — e.g. a
    // Deploy import that carried over a GUID for an API user that isn't on this environment.
    // Feeding that dangling GUID into <umb-user-input> as a "selection" leaves the picker in a
    // state its validation can't recover from when the user then tries to replace it, so we
    // treat it as unset instead and let the user pick a fresh one.
    @state()
    private _serviceAccountUnresolved = false;

    // GUIDs from the workspace's userGroups that don't resolve to a real user group on this
    // environment — e.g. a Deploy import that carried over a group the target doesn't have.
    // Feeding one of these into <umb-user-group-input> as a live selection hits the same
    // core validation bug worked around for the service account above, so we filter them out
    // of the picker's selection and warn instead of showing/saving a broken reference.
    @state()
    private _unresolvedUserGroupIds: ReadonlySet<string> = new Set();

    #workspaceContext?: typeof UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT.TYPE;
    #userItemRepository = new UmbUserItemRepository(this);
    #userGroupItemRepository = new UmbUserGroupItemRepository(this);
    #checkedServiceAccountKey?: string;
    #checkedUserGroupsKey?: string;

    constructor() {
        super();
        this.consumeContext(UA_WORKSPACE_MGMT_WORKSPACE_CONTEXT, (context) => {
            if (context) {
                this.#workspaceContext = context;
                this.observe(context.data, (model) => {
                    this._model = model;
                    this.#checkServiceAccountResolves(model?.serviceAccountKey);
                    this.#checkUserGroupsResolve(model?.userGroups);
                });
            }
        });
    }

    async #checkServiceAccountResolves(serviceAccountKey: string | undefined) {
        if (!serviceAccountKey || serviceAccountKey === UA_EMPTY_GUID) {
            this._serviceAccountUnresolved = false;
            return;
        }
        if (this.#checkedServiceAccountKey === serviceAccountKey) return;
        this.#checkedServiceAccountKey = serviceAccountKey;

        const { data } = await this.#userItemRepository.requestItems([serviceAccountKey]);

        // Ignore a stale response: a newer check may have started (or completed) for a
        // different key while this request was in flight.
        if (this.#checkedServiceAccountKey !== serviceAccountKey) return;

        this._serviceAccountUnresolved = !data?.some((user) => user.unique === serviceAccountKey);
    }

    async #checkUserGroupsResolve(userGroups: string[] | undefined) {
        const groups = userGroups ?? [];
        if (groups.length === 0) {
            this._unresolvedUserGroupIds = new Set();
            return;
        }

        const key = groups.join(",");
        if (this.#checkedUserGroupsKey === key) return;
        this.#checkedUserGroupsKey = key;

        const { data } = await this.#userGroupItemRepository.requestItems(groups);

        // Ignore a stale response, same reasoning as the service-account check above.
        if (this.#checkedUserGroupsKey !== key) return;

        const resolvedUniques = new Set(data?.map((group) => group.unique));
        this._unresolvedUserGroupIds = new Set(groups.filter((id) => !resolvedUniques.has(id)));
    }

    #onServiceAccountChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLInputElement & { selection: string[] };
        const selected = target.selection?.[0] ?? UA_EMPTY_GUID;
        this._serviceAccountUnresolved = false;
        this.#workspaceContext?.updateProperty("serviceAccountKey", selected);
    }

    #onUserGroupsChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLElement & { selection: string[] };
        this._unresolvedUserGroupIds = new Set();
        this.#workspaceContext?.updateProperty("userGroups", [...target.selection]);
    }

    #onAllowedConnectionsChange(event: CustomEvent) {
        event.stopPropagation();
        const target = event.target as HTMLElement & { selection: string[] };
        this.#workspaceContext?.updateProperty("allowedConnections", [...target.selection]);
    }

    render() {
        if (!this._model) return html`<uui-loader></uui-loader>`;

        const resolvedUserGroups = this._model.userGroups.filter(
            (id) => !this._unresolvedUserGroupIds.has(id),
        );

        return html`
            <uui-box headline=${this.localize.term("uaLabels_membership")}>
                <umb-property-layout
                    label=${this.localize.term("uaWorkspace_serviceAccountKey")}
                    description=${this.localize.term("uaWorkspace_serviceAccountDescription")}
                    mandatory
                >
                    <div slot="editor" class="picker-with-warning">
                        ${this._serviceAccountUnresolved
                            ? html`<p class="unresolved-warning">
                                  <uui-icon name="icon-alert"></uui-icon>
                                  ${this.localize.term("uaWorkspace_serviceAccountUnresolved")}
                              </p>`
                            : nothing}
                        <umb-user-input
                            max="1"
                            required
                            .selection=${!this._serviceAccountUnresolved &&
                            this._model.serviceAccountKey &&
                            this._model.serviceAccountKey !== UA_EMPTY_GUID
                                ? [this._model.serviceAccountKey]
                                : []}
                            @change=${this.#onServiceAccountChange}
                            ${umbBindToValidation(
                                this,
                                "$.serviceAccountKey",
                                this._serviceAccountUnresolved ? "" : this._model.serviceAccountKey,
                            )}
                        ></umb-user-input>
                    </div>
                </umb-property-layout>

                <umb-property-layout
                    label=${this.localize.term("uaWorkspace_userGroups")}
                    description=${this.localize.term("uaWorkspace_userGroupsDescription")}
                    mandatory
                >
                    <div slot="editor" class="picker-with-warning">
                        ${this._unresolvedUserGroupIds.size > 0
                            ? html`<p class="unresolved-warning">
                                  <uui-icon name="icon-alert"></uui-icon>
                                  ${this.localize.term(
                                      "uaWorkspace_userGroupsUnresolved",
                                      this._unresolvedUserGroupIds.size,
                                  )}
                              </p>`
                            : nothing}
                        <umb-user-group-input
                            required
                            .selection=${resolvedUserGroups}
                            @change=${this.#onUserGroupsChange}
                            ${umbBindToValidation(this, "$.userGroups", resolvedUserGroups)}
                        ></umb-user-group-input>
                    </div>
                </umb-property-layout>

                <umb-property-layout
                    label=${this.localize.term("uaWorkspace_allowedConnections")}
                    description=${this.localize.term("uaWorkspace_allowedConnectionsDescription")}
                >
                    <ua-input-connection
                        slot="editor"
                        .selection=${this._model.allowedConnections}
                        @change=${this.#onAllowedConnectionsChange}
                    ></ua-input-connection>
                </umb-property-layout>
            </uui-box>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            :host {
                display: block;
                padding: var(--uui-size-layout-1);
            }

            uui-box {
                --uui-box-default-padding: 0 var(--uui-size-space-5);
            }

            .picker-with-warning {
                display: flex;
                flex-direction: column;
                gap: var(--uui-size-space-3);
            }

            .unresolved-warning {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
                margin: 0;
                color: var(--uui-color-warning-standalone, var(--uui-color-warning));
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

export default UaWorkspaceSettingsWorkspaceViewElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-workspace-settings-workspace-view": UaWorkspaceSettingsWorkspaceViewElement;
    }
}
