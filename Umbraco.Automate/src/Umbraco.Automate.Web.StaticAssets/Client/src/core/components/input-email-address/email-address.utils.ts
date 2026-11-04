/** Separators that end an address while one is being typed or pasted, as in an email client. */
export const TYPED_SEPARATORS = ",;";

/**
 * Splits `value` on any of `separators`, except inside double quotes or braces, so
 * `"Doe, John" <j@example.com>` stays one entry, and so does a placeholder such as
 * `${ binding }` that is filled in later. Parts are returned as-is, untrimmed and including
 * empty ones, so a caller can tell whether the text ended on a separator.
 */
export function splitList(value: string, separators: string): string[] {
    const parts: string[] = [];
    let current = "";
    let inQuotes = false;
    let braceDepth = 0;

    for (let i = 0; i < value.length; i++) {
        const ch = value[i];

        if (inQuotes && ch === "\\" && i + 1 < value.length) {
            current += ch + value[++i];
            continue;
        }

        if (inQuotes) {
            if (ch === '"') inQuotes = false;
        } else if (ch === "{") {
            braceDepth++;
        } else if (ch === "}" && braceDepth > 0) {
            braceDepth--;
        } else if (braceDepth === 0 && ch === '"') {
            inQuotes = true;
        } else if (braceDepth === 0 && separators.includes(ch)) {
            parts.push(current);
            current = "";
            continue;
        }

        current += ch;
    }

    parts.push(current);
    return parts;
}

/**
 * Splits an address list into its entries, trimmed and without empty ones. Only commas separate
 * stored entries by default, which is what .NET's `MailAddressCollection` and MimeKit expect.
 */
export function splitAddresses(value: string | null | undefined, separators = ","): string[] {
    if (!value) return [];
    return splitList(value, separators)
        .map((entry) => entry.trim())
        .filter((entry) => entry.length > 0);
}

/** Joins entries back into a comma-separated address list. */
export function joinAddresses(entries: string[]): string {
    return entries.join(", ");
}

const ADDRESS = String.raw`[^\s@<>",;]+@[^\s@<>",;]+`;
const BARE_ADDRESS = new RegExp(`^${ADDRESS}$`);
const NAMED_ADDRESS = new RegExp(String.raw`^(?:"(?:[^"\\]|\\.)*"|[^"<>@,;]*)\s*<${ADDRESS}>$`);

/**
 * Whether `entry` reads as one address, bare or with a display name. A hint for the editor
 * rather than the rule: whatever sends the email decides what it accepts.
 */
export function isEmailAddress(entry: string): boolean {
    return BARE_ADDRESS.test(entry) || NAMED_ADDRESS.test(entry);
}
