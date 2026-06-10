const localizationManifests: UmbExtensionManifest[] = [
    {
        type: "localization",
        alias: "Ua.Localization.En",
        weight: -100,
        name: "English",
        meta: {
            culture: "en",
        },
        js: () => import("./en.js"),
    },
];

export const manifests: UmbExtensionManifest[] = [...localizationManifests];
