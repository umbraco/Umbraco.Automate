import { css, html, customElement, property, state } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { createRoot, type Root } from "react-dom/client";
import { createElement } from "react";
import type { Node, Edge, Viewport, ColorMode, NodeTypes, EdgeTypes } from "@xyflow/react";
import AutomationCanvas from "./AutomationCanvas.js";
import type { CanvasChangeDetail, AddNodeRequestDetail, NodeDeleteRequestDetail } from "./types.js";
import { UMB_THEME_CONTEXT } from "@umbraco-cms/backoffice/themes";

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

    @property({ type: Boolean, attribute: "read-only" })
    readOnly = false;

    @property({ attribute: false })
    nodeTypes?: NodeTypes;

    @property({ attribute: false })
    edgeTypes?: EdgeTypes;

    @property({ attribute: false })
    extraStyles?: string;

    @state()
    private _mounted = false;

    @state()
    private _colorMode: ColorMode = "light";

    #root: Root | null = null;
    #container: HTMLDivElement | null = null;

    constructor() {
        super();
        this.consumeContext(UMB_THEME_CONTEXT, (context) => {
            if (!context) return;
            this.observe(context.theme, (themeAlias) => {
                this._colorMode = themeAlias?.includes("dark") ? "dark" : "light";
                if (this._mounted) {
                    this.#renderReact();
                }
            });
        });
    }

    override firstUpdated() {
        // Inject React Flow + custom styles into the shadow root
        const styleEl = document.createElement("style");
        styleEl.textContent = reactFlowCss + "\n" + canvasCss + (this.extraStyles ? "\n" + this.extraStyles : "");
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
        if (this._mounted && (changedProperties.has("nodes") || changedProperties.has("edges") || changedProperties.has("viewport") || changedProperties.has("readOnly") || changedProperties.has("nodeTypes") || changedProperties.has("edgeTypes"))) {
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
                colorMode: this._colorMode,
                readOnly: this.readOnly,
                nodeTypes: this.nodeTypes,
                edgeTypes: this.edgeTypes,
                onCanvasChange: this.#onCanvasChange,
                onAddNodeRequest: this.#onAddNodeRequest,
                onDeleteRequest: this.#onDeleteRequest,
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

    #onDeleteRequest = (nodes: import("@xyflow/react").Node[]): Promise<boolean> => {
        return new Promise<boolean>((resolve) => {
            this.dispatchEvent(
                new CustomEvent<NodeDeleteRequestDetail>("ua:node-delete-request", {
                    bubbles: true,
                    composed: true,
                    detail: { nodes, resolve },
                }),
            );
        });
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
