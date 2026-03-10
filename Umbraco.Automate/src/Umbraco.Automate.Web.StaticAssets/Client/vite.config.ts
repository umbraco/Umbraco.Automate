import { defineConfig } from "vite";
import { resolve } from "path";

export default defineConfig({
    build: {
        lib: {
            entry: {
                "umbraco-automate-manifests": resolve(__dirname, "src/manifests.ts"),
                "umbraco-automate-app": resolve(__dirname, "src/app.ts"),
            },
            formats: ["es"],
        },
        outDir: "../wwwroot",
        emptyOutDir: true,
        sourcemap: true,
        rollupOptions: {
            external: [/^@umbraco/],
        },
    },
});
