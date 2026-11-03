import { APIResponse } from '@playwright/test';

/**
 * Throws with the server's status and message when an API call did not succeed.
 *
 * Every helper that creates, reads or updates something checks its response with this. Otherwise
 * a failed setup call shows up later as an unrelated error, such as "Unexpected end of JSON input"
 * from the next read, and the spec fails a step after the real cause.
 *
 * `action` describes the call in a few words, for example `Creating workspace "Demo"`.
 */
export async function ensureOk(response: APIResponse, action: string): Promise<void> {
  if (!response.ok()) {
    throw new Error(`${action} failed (${response.status()}): ${await response.text()}`);
  }
}
