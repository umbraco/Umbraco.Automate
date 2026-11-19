import { createContext, useContext } from "react";

/**
 * Bridges the backoffice localization (owned by the Lit host element) into the React canvas.
 * Same behaviour as `UmbLocalizationController.string`: `#key` resolves to a term, anything
 * else is returned as-is. The result is always rendered as text, never as markup.
 */
export type LocalizeString = (value: string) => string;

const identity: LocalizeString = (value) => value;

export const LocalizeContext = createContext<LocalizeString>(identity);

export function useLocalizeString(): LocalizeString {
    return useContext(LocalizeContext);
}
