import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { createRoot, type Root } from "react-dom/client";
import { createElement } from "react";
import type { Node, Edge, Viewport } from "@xyflow/react";
import AutomationCanvas from "./AutomationCanvas.js";
import type { CanvasChangeDetail, AddNodeRequestDetail } from "./types.js";

// Import CSS as strings (Vite ?inline) so we can inject into shadow DOM.
import reactFlowCss from "@xyflow/react/dist/style.css?inline";
import canvasCss from "./canvas.styles.css?inline";

@customElement("ua-automation-canvas")
export class UaAutomationCanvasElement extends UmbLitElement {
    @property({ type: Array })
    nodes: Node[] = [];

    @property({ type: Array })
    edges: Edge[] = [];

    @property({ type: Object })
    viewport?: Viewport;

    @state()
    private _mounted = false;

    #root: Root | null = null;
    #container: HTMLDivElement | null = null;

    override firstUpdated() {
        // Inject React Flow + custom styles into the shadow root
        const styleEl = document.createElement("style");
        styleEl.textContent = reactFlowCss + "\n" + canvasCss;
        this.shadowRoot!.prepend(styleEl);

        this.#container = this.shadowRoot!.querySelector<HTMLDivElement>("#canvas-container");
        if (this.#container) {
            this.#root = createRoot(this.#container);
            this._mounted = true;
            this.#renderReact();
        }
    }

    override updated(changedProperties: Map<string, unknown>) {
        super.updated(changedProperties);
        if (this._mounted && (changedProperties.has("nodes") || changedProperties.has("edges") || changedProperties.has("viewport"))) {
            this.#renderReact();
        }
    }

    override disconnectedCallback() {
        super.disconnectedCallback();
        this.#root?.unmount();
        this.#root = null;
        this._mounted = false;
    }

    #renderReact() {
        if (!this.#root) return;

        this.#root.render(
            createElement(AutomationCanvas, {
                nodes: this.nodes,
                edges: this.edges,
                viewport: this.viewport,
                onCanvasChange: this.#onCanvasChange,
                onAddNodeRequest: this.#onAddNodeRequest,
            }),
        );
    }

    #onCanvasChange = (detail: CanvasChangeDetail) => {
        this.dispatchEvent(
            new CustomEvent("ua:canvas-change", {
                bubbles: true,
                composed: true,
                detail,
            }),
        );
    };

    #onAddNodeRequest = (detail: AddNodeRequestDetail) => {
        this.dispatchEvent(
            new CustomEvent("ua:add-node-request", {
                bubbles: true,
                composed: true,
                detail,
            }),
        );
    };

    override render() {
        return html`<div id="canvas-container"></div>`;
    }

    static override styles = [
        css`
            :host {
                display: block;
                width: 100%;
                height: 100%;
            }

            #canvas-container {
                width: 100%;
                height: 100%;
            }
        `,
    ];
}

export default UaAutomationCanvasElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-automation-canvas": UaAutomationCanvasElement;
    }
}
