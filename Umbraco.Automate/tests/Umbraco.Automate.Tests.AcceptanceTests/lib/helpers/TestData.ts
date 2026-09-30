/**
 * Small helpers for generating test data.
 *
 * Workspaces have **no** alias uniqueness check on the server (only workspace *groups* validate
 * unique names), so a fixed alias silently accumulates litter when a run dies before teardown.
 * Every fixture therefore generates a unique name and alias per run.
 */

/* Short, collision-resistant enough for a single-worker suite, and readable in the backoffice. */
export function uniqueSuffix(): string {
  return Date.now().toString(36) + Math.random().toString(36).slice(2, 6);
}

export function uniqueName(prefix: string): string {
  return `${prefix} ${uniqueSuffix()}`;
}

/* The alias is documented as "unique, URL-safe" (used for Deploy transfer). */
export function toAlias(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}
