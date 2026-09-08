import { css, html, customElement, property, nothing } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import type { EditableModelFieldDescriptorModel } from "../../../api/types.gen.js";
import { UA_EMPTY_GUID } from "../../../core/index.js";

import "../../../core/components/mcp-url-field/mcp-url-field.element.js";
import "../../../core/components/settings-form/settings-form.element.js";

/**
 * MCP-specific extras for the trigger settings sidebar, grouped in one box: the tool
 * endpoint URL (so it can be copied while wiring up an MCP client) first, then the
 * trigger's own test-arguments field used to exercise the automation on demand.
 * Mirrors {@link UaWebhookTriggerPanelElement}.
 */
@customElement("ua-mcp-trigger-panel")
export class UaMcpTriggerPanelElement extends UmbLitElement {
    /** Unique of the automation this MCP tool belongs to. */
    @property({ type: String, attribute: "automation-id" })
    automationId = "";

    /** The MCP trigger's own settings fields (test arguments) to render below the URL. */
    @property({ type: Array })
    testFields: EditableModelFieldDescriptorModel[] = [];

    /** The trigger's current settings values, keyed by field alias. */
    @property({ type: Object })
    values: Record<string, unknown> = {};

    get #isSaved(): boolean {
        return !!this.automationId && this.automationId !== UA_EMPTY_GUID;
    }

    override render() {
        return html`
            <uui-box headline=${this.localize.term("uaMcp_headline")}>
                ${this.#isSaved ? this.#renderUrl() : this.#renderUnsavedHint()}
                ${this.#isSaved ? this.#renderTestFields() : nothing}
            </uui-box>
        `;
    }

    #renderUnsavedHint() {
        return html`<p class="hint">${this.localize.term("uaMcp_unsavedHint")}</p>`;
    }

    #renderUrl() {
        return html`
            <umb-property-layout label=${this.localize.term("uaLabels_mcpUrl")} orientation="vertical">
                <ua-mcp-url-field slot="editor" automation-id=${this.automationId}></ua-mcp-url-field>
            </umb-property-layout>
        `;
    }

    #renderTestFields() {
        if (!this.testFields.length) return nothing;

        // Bubbles a `ua:settings-change` event the modal listens for directly on this element,
        // so it crosses this panel's shadow boundary without the panel itself relaying it.
        return html`
            <ua-settings-form no-box label-on-top .fields=${this.testFields} .values=${this.values}></ua-settings-form>
        `;
    }

    static styles = [
        UmbTextStyles,
        css`
            /* Match ua-settings-form so the panel's rows sit on the same rhythm as the
               settings fields directly above it. */
            umb-property-layout {
                --uui-size-layout-1: var(--uui-size-space-2);
            }

            .hint {
                margin: 0;
                color: var(--uui-color-text-alt);
            }
        `,
    ];
}

export default UaMcpTriggerPanelElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-mcp-trigger-panel": UaMcpTriggerPanelElement;
    }
}
