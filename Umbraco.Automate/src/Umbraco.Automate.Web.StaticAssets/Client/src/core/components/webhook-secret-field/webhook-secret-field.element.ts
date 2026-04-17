import { css, customElement, html, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbFormControlMixin } from "@umbraco-cms/backoffice/validation";
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationContext } from "@umbraco-cms/backoffice/notification";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";

/** 32 random bytes → 43-char URL-safe base64 string, same strength as the server-side fallback. */
function generateSecret(): string {
    const bytes = new Uint8Array(32);
    crypto.getRandomValues(bytes);
    let binary = "";
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
}

@customElement("ua-webhook-secret-field")
export class UaWebhookSecretFieldElement
    extends UmbFormControlMixin<string, typeof UmbLitElement, undefined>(UmbLitElement, undefined)
    implements UmbPropertyEditorUiElement
{
    @property({ type: Boolean, reflect: true })
    readonly = false;

    #notificationContext?: UmbNotificationContext;
    // Flag set when *we* updated the value (seed / regenerate / user typing) so the
    // next updated() call skips the auto-seed check. External resets (e.g. the picker
    // wiping settings when the strategy changes) leave this false and get re-seeded.
    #selfUpdated = false;

    constructor() {
        super();
        this.consumeContext(UMB_NOTIFICATION_CONTEXT, (context) => {
            this.#notificationContext = context;
        });
    }

    protected override updated() {
        if (this.#selfUpdated) {
            this.#selfUpdated = false;
            return;
        }
        if (!this.value && !this.readonly) {
            this.#assignNewSecret();
        }
    }

    #assignNewSecret() {
        this.#selfUpdated = true;
        this.value = generateSecret();
        this.dispatchEvent(new UmbChangeEvent());
    }

    #onRegenerate() {
        this.#assignNewSecret();
        this.#notify("positive", "New secret generated. Remember to save.");
    }

    async #onCopy() {
        if (!this.value) return;
        try {
            await navigator.clipboard.writeText(this.value);
            this.#notify("positive", "Secret copied to clipboard.");
        } catch {
            this.#notify("danger", "Could not copy secret. Select the value manually.");
        }
    }

    #onInput(e: InputEvent) {
        const next = (e.target as HTMLInputElement).value;
        if (next === this.value) return;
        this.#selfUpdated = true;
        this.value = next;
        this.dispatchEvent(new UmbChangeEvent());
    }

    #notify(color: "positive" | "danger", message: string) {
        this.#notificationContext?.peek(color, { data: { message } });
    }

    override render() {
        return html`
            <div class="row">
                <uui-input
                    .value=${this.value ?? ""}
                    ?readonly=${this.readonly}
                    @input=${this.#onInput}
                ></uui-input>
                <uui-button
                    compact
                    look="secondary"
                    label="Copy secret"
                    ?disabled=${!this.value}
                    @click=${this.#onCopy}
                >
                    <uui-icon name="icon-clipboard-copy"></uui-icon>
                </uui-button>
                <uui-button
                    compact
                    look="secondary"
                    label="Regenerate secret"
                    ?disabled=${this.readonly}
                    @click=${this.#onRegenerate}
                >
                    <uui-icon name="icon-refresh"></uui-icon>
                </uui-button>
            </div>
        `;
    }

    static override styles = [
        css`
            :host {
                display: block;
            }

            .row {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-2);
            }

            uui-input {
                flex: 1;
                font-family: var(--uui-font-family-mono, monospace);
            }
        `,
    ];
}

export default UaWebhookSecretFieldElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-webhook-secret-field": UaWebhookSecretFieldElement;
    }
}
