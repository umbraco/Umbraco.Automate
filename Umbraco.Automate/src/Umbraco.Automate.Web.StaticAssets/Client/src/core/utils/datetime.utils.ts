export function formatDateTime(input: string | Date, locale: string = "en-US"): string {
    const date = typeof input === "string" ? new Date(input) : input;

    if (isNaN(date.getTime())) {
        throw new Error("Invalid date input");
    }

    const formatter = new Intl.DateTimeFormat(locale, {
        month: "long",
        day: "numeric",
        year: "numeric",
        hour: "numeric",
        minute: "2-digit",
        second: "2-digit",
        hour12: true,
    });

    const parts = formatter.formatToParts(date);
    const map: Partial<Record<Intl.DateTimeFormatPartTypes, string>> = {};

    for (const part of parts) {
        map[part.type] = part.value;
    }

    return `${map.month} ${map.day}, ${map.year} at ${map.hour}:${map.minute}:${map.second} ${map.dayPeriod}`;
}

/**
 * Formats a date as `yyyy-MM-dd HH:mm:ss.SSS` in the local timezone — used for log entry
 * timestamps, where a fixed-width, sortable, millisecond-precision format is preferable to
 * the locale-formatted long form used by {@link formatDateTime}.
 */
export function formatLogTimestamp(input: string | Date): string {
    const date = typeof input === "string" ? new Date(input) : input;

    if (isNaN(date.getTime())) {
        throw new Error("Invalid date input");
    }

    const pad = (value: number, length = 2) => value.toString().padStart(length, "0");

    const year = date.getFullYear();
    const month = pad(date.getMonth() + 1);
    const day = pad(date.getDate());
    const hours = pad(date.getHours());
    const minutes = pad(date.getMinutes());
    const seconds = pad(date.getSeconds());
    const milliseconds = pad(date.getMilliseconds(), 3);

    return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}.${milliseconds}`;
}
