import { css, html, customElement, property } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";

export interface UaStatusCardData {
    label: string;
    count: number;
    color: string;
    icon: string;
}

@customElement("ua-status-cards")
export class UaStatusCardsElement extends UmbLitElement {
    @property({ attribute: false })
    cards: UaStatusCardData[] = [];

    override render() {
        return html`
            <div class="cards">
                ${this.cards.map(
                    (card) => html`
                        <div class="card">
                            <div class="card-icon" style="color: var(--uui-color-${card.color});">
                                <uui-icon name=${card.icon}></uui-icon>
                            </div>
                            <div class="card-content">
                                <span class="card-count">${card.count}</span>
                                <span class="card-label">${card.label}</span>
                            </div>
                        </div>
                    `,
                )}
            </div>
        `;
    }

    static override styles = [
        UmbTextStyles,
        css`
            .cards {
                display: grid;
                grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                gap: var(--uui-size-space-5);
            }

            .card {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-4);
                padding: var(--uui-size-space-5);
                background: var(--uui-color-surface);
                border: 1px solid var(--uui-color-border);
                border-radius: var(--uui-border-radius);
            }

            .card-icon {
                font-size: var(--uui-size-8);
            }

            .card-content {
                display: flex;
                flex-direction: column;
            }

            .card-count {
                font-size: var(--uui-size-8);
                font-weight: 700;
                line-height: 1;
            }

            .card-label {
                font-size: var(--uui-size-4);
                color: var(--uui-color-text-alt);
                margin-top: var(--uui-size-space-1);
            }
        `,
    ];
}

declare global {
    interface HTMLElementTagNameMap {
        "ua-status-cards": UaStatusCardsElement;
    }
}
