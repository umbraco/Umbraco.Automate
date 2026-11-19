/**
 * Writes the Playwright --test-list file for one CI shard, splitting the suite by recorded test
 * duration rather than by test count.
 *
 *   node shard-test-list.js <current>/<total> <outFile>
 *   npm run test -- --test-list <outFile>
 *
 * Each full-run leg of the CI stage (devops/ci/acceptance-tests.yml) runs exactly that, e.g.
 * `node shard-test-list.js 1/4 playwright/shard-test-list.txt && npm run test -- --test-list
 * playwright/shard-test-list.txt`. See CLAUDE.md, "CI shards are split by duration".
 *
 * Playwright's own --shard gives every shard the same NUMBER of tests, in file order, so shard 1
 * always got the slow automation specs and finished last: medians of 55 runs (November 2026) were
 * 824, 738, 711 and 580 s for the four shards.
 *
 * Every shard runs this script with the same inputs, so they all compute the same split and each
 * keeps its own part. The tests come from `playwright test --list`, not from the timings file, so
 * every test in the suite lands in exactly one shard: a test with no recorded time (a new one)
 * counts as the median, and stale timings only make the split less even, never drop a test.
 * Refresh the timings with update-shard-timings.js.
 */
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execFileSync } = require('child_process');

const PLAYWRIGHT_CLI = require.resolve('@playwright/test/cli');
// What package.json's `test` script runs (`npx playwright test DefaultConfig`), so the split lists
// the same tests the shard then runs. Keep the two in step.
const TEST_ARGS = ['test', 'DefaultConfig'];
const TIMINGS_FILE = path.join(__dirname, 'shard-timings.json');
// Used only if the timings file has no entries at all.
const FALLBACK_SECONDS = 10;
// Files whose tests must stay on one shard, as --shard would keep them: a serial or default-mode
// describe shares state or relies on order between its tests.
const KEEP_TOGETHER = /\bdescribe\.serial\b|\bmode\s*:\s*['"](?:serial|default)['"]/;

function parseShard(arg) {
  const match = /^(\d+)\/(\d+)$/.exec(arg || '');
  const current = match && Number(match[1]);
  const total = match && Number(match[2]);
  if (!match || total < 1 || current < 1 || current > total) {
    throw new Error(`Expected <current>/<total>, for example 1/4; got "${arg}"`);
  }
  return { current, total };
}

/**
 * Every test the `test` script would run, as { project, file, key }; key is
 * "file › describe › title". --no-deps leaves out the setup project, which Playwright runs on
 * every shard anyway. The JSON goes to a file, not stdout: dotenv logs to stdout when the config
 * loads it.
 */
function listTests() {
  const report = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'shard-list-')), 'list.json');
  execFileSync(process.execPath, [PLAYWRIGHT_CLI, ...TEST_ARGS, '--list', '--no-deps', '--reporter=json'], {
    cwd: __dirname,
    env: { ...process.env, PLAYWRIGHT_JSON_OUTPUT_FILE: report },
    stdio: ['ignore', 'ignore', 'inherit'],
  });
  const json = JSON.parse(fs.readFileSync(report, 'utf8'));
  if (json.errors && json.errors.length) {
    throw new Error(`playwright --list reported errors:\n${json.errors.map((e) => e.message).join('\n')}`);
  }
  const tests = [];
  const walk = (suite, file, titles) => {
    for (const spec of suite.specs || []) {
      for (const test of spec.tests) {
        tests.push({ project: test.projectName, file, key: [file, ...titles, spec.title].join(' › ') });
      }
    }
    for (const child of suite.suites || []) walk(child, file, [...titles, child.title]);
  };
  for (const fileSuite of json.suites) {
    walk(fileSuite, fileSuite.file.replace(/\\/g, '/'), []);
  }
  return { tests, rootDir: json.config.rootDir, fullyParallel: json.config.fullyParallel === true };
}

function median(values) {
  if (!values.length) return FALLBACK_SECONDS;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor((sorted.length - 1) / 2)];
}

/** Splits the suite for shard `current`/`total` and writes its --test-list to outFile. */
function writeShardTestList(shardArg, outFile) {
  const { current, total } = parseShard(shardArg);
  if (!outFile) throw new Error('Expected an output file after <current>/<total>');

  const timings = fs.existsSync(TIMINGS_FILE) ? JSON.parse(fs.readFileSync(TIMINGS_FILE, 'utf8')) : {};
  const fallback = median(Object.values(timings));
  const { tests, rootDir, fullyParallel } = listTests();

  // A unit is one test, or a whole file whose tests must stay together.
  const wholeFiles = new Set();
  const keepWhole = (file) => !fullyParallel || KEEP_TOGETHER.test(fs.readFileSync(path.join(rootDir, file), 'utf8'));
  for (const file of new Set(tests.map((t) => t.file))) {
    if (keepWhole(file)) wholeFiles.add(file);
  }
  const units = new Map();
  let untimed = 0;
  for (const test of tests) {
    const line = `[${test.project}] › ${test.key}`;
    const id = wholeFiles.has(test.file) ? `[${test.project}] › ${test.file}` : line;
    if (!units.has(id)) units.set(id, { id, seconds: 0, lines: [] });
    const unit = units.get(id);
    if (!(test.key in timings)) untimed++;
    unit.seconds += timings[test.key] ?? fallback;
    unit.lines.push(line);
  }

  // Longest first, each to the shard with the least work so far. Ties break on the name and the
  // lowest shard number, so every shard computes the same split.
  const byWeight = [...units.values()].sort(
    (a, b) => b.seconds - a.seconds || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const shards = Array.from({ length: total }, () => ({ seconds: 0, lines: [] }));
  for (const unit of byWeight) {
    const target = shards.reduce((best, shard) => (shard.seconds < best.seconds ? shard : best));
    target.seconds += unit.seconds;
    target.lines.push(...unit.lines);
  }

  const mine = shards[current - 1].lines.sort();
  fs.mkdirSync(path.dirname(path.resolve(outFile)), { recursive: true });
  fs.writeFileSync(outFile, mine.join('\n') + '\n');

  console.log(`${tests.length} tests; ${untimed} without a recorded time (counted as ${fallback.toFixed(1)} s)` +
    (wholeFiles.size ? `; ${wholeFiles.size} files kept whole` : ''));
  shards.forEach((shard, i) => {
    const marker = i === current - 1 ? '  <- this shard' : '';
    console.log(`  shard ${i + 1}/${total}: ${shard.lines.length} tests, ~${(shard.seconds / 60).toFixed(1)} min${marker}`);
  });
  console.log(`Wrote ${mine.length} tests to ${outFile}`);
}

module.exports = { TIMINGS_FILE, listTests };

if (require.main === module) writeShardTestList(process.argv[2], process.argv[3]);
