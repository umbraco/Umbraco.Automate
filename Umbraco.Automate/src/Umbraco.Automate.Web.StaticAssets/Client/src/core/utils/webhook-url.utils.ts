/**
 * Builds the public webhook endpoint URL for an automation. Mirrors the route registered by
 * the server's `WebhookEndpointController` (`automate/webhook/{automationId}`), so keep the
 * two in step.
 */
export function buildWebhookUrl(automationId: string): string {
    return `${window.location.origin}/automate/webhook/${automationId}`;
}
