/**
 * Refreshes shard-timings.json, the per-test durations shard-test-list.js splits the CI shards by,
 * from the acceptance results that pipeline builds published to Azure DevOps.
 *
 *   node update-shard-timings.js <buildId> [<buildId> ...]
 *
 * Pass a few recent green builds of this line's dev branch that ran the whole suite in shards (the
 * number in the build URL, ?buildId=...). They must be builds of the Umbraco.Automate pipeline
 * (714). Each test's time is the median of its passed results in those builds' "Acceptance Tests
 * (Shard i of n)" runs. Only tests in the current suite (`playwright test --list`) are kept, and
 * tests with no passed result are listed. Needs this folder's npm packages and the Azure CLI,
 * signed in (`az login`).
 *
 * Run it when the suite has changed a lot, or when the shard times in the "Run acceptance tests"
 * logs have drifted apart. Out-of-date timings never drop a test, they only make the split less
 * even.
 */
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { listTests, TIMINGS_FILE } = require('./shard-test-list');

const PROJECT_URL = 'https://dev.azure.com/umbraco/Umbraco%20Automate';
const PIPELINE_ID = 714;
// The Azure DevOps resource id the Azure CLI gets a token for.
const AZURE_DEVOPS_RESOURCE = '499b84ac-1321-427f-aa17-267ca6975798';
// Each leg's test run is "Acceptance Tests (<leg>)" (publish-acceptance-results.yml@build in
// devops/ci/acceptance-tests.yml), and the shard legs are "Shard i of n".
const SHARD_RUN = /\(Shard \d+ of \d+\)$/;
const PAGE = 1000;

let authorization;
async function get(url) {
  authorization ??= `Bearer ${execSync(
    `az account get-access-token --resource ${AZURE_DEVOPS_RESOURCE} --query accessToken -o tsv`,
    { encoding: 'utf8' },
  ).trim()}`;
  const response = await fetch(`${PROJECT_URL}/_apis/${url}`, { headers: { Authorization: authorization } });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${url}`);
  return response.json();
}

// The JUnit results Azure DevOps stores lose the "›" between titles (it comes back as U+FFFD), so
// put it back to match the keys shard-test-list.js builds.
const normalise = (name) => name.replace(/ (?:›|�) /g, ' › ');

async function main() {
  const buildIds = process.argv.slice(2);
  if (!buildIds.length || buildIds.some((id) => !/^\d+$/.test(id))) {
    throw new Error('Usage: node update-shard-timings.js <buildId> [<buildId> ...]');
  }

  const samples = {};
  for (const buildId of buildIds) {
    const build = await get(`build/builds/${buildId}?api-version=7.1`);
    if (build.definition.id !== PIPELINE_ID) {
      throw new Error(`Build ${buildId} is from pipeline ${build.definition.id}, not ${PIPELINE_ID}`);
    }
    const runs = (await get(`test/runs?buildUri=vstfs:///Build/Build/${buildId}&api-version=7.1`)).value
      .filter((run) => SHARD_RUN.test(run.name));
    if (!runs.length) throw new Error(`Build ${buildId} published no shard results`);
    for (const run of runs) {
      for (let skip = 0; ; skip += PAGE) {
        const results = (await get(`test/Runs/${run.id}/results?$top=${PAGE}&$skip=${skip}&api-version=7.1`)).value;
        for (const result of results) {
          if (result.outcome !== 'Passed' || result.durationInMs == null) continue;
          const key = `${result.automatedTestStorage.replace(/\\/g, '/')} › ${normalise(result.automatedTestName)}`;
          (samples[key] ??= []).push(result.durationInMs / 1000);
        }
        if (results.length < PAGE) break;
      }
    }
    console.log(`Build ${buildId} (${build.sourceBranch}): ${runs.length} shard runs`);
  }

  const timings = {};
  const keys = [...new Set(listTests().tests.map((t) => t.key))].sort();
  for (const key of keys) {
    const values = samples[key];
    if (!values) continue;
    const sorted = [...values].sort((a, b) => a - b);
    timings[key] = Math.round(sorted[Math.floor((sorted.length - 1) / 2)] * 10) / 10;
  }
  fs.writeFileSync(TIMINGS_FILE, JSON.stringify(timings, null, 2) + '\n');
  const missing = keys.filter((key) => !(key in timings));
  console.log(`Wrote ${Object.keys(timings).length} of ${keys.length} tests to ${path.basename(TIMINGS_FILE)}`);
  if (missing.length) console.log(`No passed result for:\n  ${missing.join('\n  ')}`);
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
