import chalk from "chalk";
import https from "https";
import { createClient, defaultPlugins } from "@hey-api/openapi-ts";
import { getPort } from "worktree-dev-port";

console.log(chalk.green("Generating OpenAPI client..."));

const documentName = process.argv[2];
if (documentName === undefined) {
    console.error(chalk.red(`ERROR: Missing OpenAPI document name`));
    console.error(`Please provide the document name as the first argument found in ${chalk.yellow("package.json")}`);
    console.error(`Example: node generate-openapi.js ${chalk.yellow("automate-management")}`);
    process.exit(1);
}

const openApiPath = `umbraco/swagger/${documentName}/swagger.json`;

// Get the dev port already assigned to this worktree by the demo site (via
// Umbraco.Community.WorktreeDevPort). The site must already be running.
let port;
try {
    port = getPort();
} catch (error) {
    console.error(chalk.red(`ERROR: ${error.message}`));
    console.error(`Start the demo site first: ${chalk.yellow("/demo-site-management start")}`);
    process.exit(1);
}

console.log(chalk.cyan(`Using port ${port} for this worktree`));
console.log(`Fetching ${chalk.yellow(`https://127.0.0.1:${port}/${openApiPath}`)}`);

// Fetch the spec over HTTPS using the ASP.NET Core dev cert (self-signed, so skip verification).
// The Host header is pinned to "localhost" (rather than the real 127.0.0.1:<port>) so the server's
// generated OpenAPI doc — and the client baseUrl hey-api derives from it — doesn't bake in this
// worktree's own port.
const specData = await new Promise((resolve, reject) => {
    https
        .get(
            {
                hostname: "127.0.0.1",
                port,
                path: `/${openApiPath}`,
                rejectUnauthorized: false,
                headers: { Host: "localhost" },
            },
            (res) => {
                let data = "";
                res.setEncoding("utf8");
                res.on("data", (chunk) => (data += chunk));
                res.on("end", () => {
                    res.statusCode === 200
                        ? resolve(data)
                        : reject(new Error(`HTTP ${res.statusCode} ${res.statusMessage}`));
                });
            },
        )
        .on("error", reject);
});

console.log(`OpenAPI spec fetched successfully`);
console.log(`Calling ${chalk.yellow("hey-api")} to generate TypeScript client`);

try {
    await createClient({
        input: JSON.parse(specData),
        output: "src/api",
        plugins: [
            ...defaultPlugins,
            "@hey-api/client-fetch",
            {
                name: "@hey-api/sdk",
                operations: {
                    strategy: "byTags",
                    container: "class",
                    containerName: "{{name}}Service",
                },
            },
        ],
    });

    console.log(chalk.green("✓ TypeScript client generated successfully"));
} catch (error) {
    console.error(`ERROR: Failed to generate client: ${chalk.red(error.message)}`);
    process.exit(1);
}
