// Writes .env for the acceptance test suite.
//
// Each git worktree's demo site gets its own stable port, assigned by the
// Umbraco.Community.WorktreeDevPort package and stored in that worktree's own git config under
// wdp.port. This script reads it with the same `git config` call the package's npm helper uses,
// and only falls back to prompting if no port has been assigned yet.
//
// Run: npm run config
//   Optional: --worktree <path> to target a demo site started from a different worktree or
//   checkout (any path inside it).
//   Optional: --port <number> to skip discovery and use that port.
//   Optional: --yes to accept every default without prompting.

const fs = require('fs');
const https = require('https');
const path = require('path');
const { execSync } = require('child_process');
const prompt = require('prompt');

const DEFAULT_LOGIN = 'admin@example.com';
const DEFAULT_PASSWORD = 'password1234';
const DEFAULT_URL = 'https://localhost:44380';

function getArg(name) {
  const argIndex = process.argv.indexOf(name);
  return argIndex !== -1 ? process.argv[argIndex + 1] : undefined;
}

function getAssignedPort() {
  const port = getArg('--port');
  if (port) {
    return parseInt(port, 10);
  }

  const cwd = getArg('--worktree') || process.cwd();
  try {
    const value = execSync('git config --worktree --get wdp.port', {
      cwd,
      encoding: 'utf-8',
      stdio: ['ignore', 'pipe', 'ignore']
    }).trim();
    return value ? parseInt(value, 10) : null;
  } catch {
    // Exits non-zero when the key is unset, i.e. the demo site has never started here.
    return null;
  }
}

// The dev certificate is self-signed, so skip verification; any status code means it is up.
function isSiteUp(port) {
  return new Promise((resolve) => {
    const request = https.get(
      { hostname: 'localhost', port, path: '/', rejectUnauthorized: false },
      (res) => {
        res.resume();
        resolve(true);
      }
    );
    request.on('error', () => resolve(false));
    request.setTimeout(3000, () => {
      request.destroy();
      resolve(false);
    });
  });
}

async function main() {
  const port = getAssignedPort();
  let url = null;

  if (port) {
    // The backoffice needs https, because OpenIddict rejects plain http.
    url = `https://localhost:${port}`;
    if (await isSiteUp(port)) {
      console.log(`Found demo site at ${url}`);
    } else {
      console.log(`Nothing answered at ${url}.`);
      console.log('Start it with /demo-site-management start before running the tests.');
    }
  } else {
    console.log('No demo site port assigned to this worktree yet (git config wdp.port is unset).');
    console.log('Start the demo site once to assign one, or pass --port <number>.');
  }

  const defaults = {
    url: url || DEFAULT_URL,
    login: DEFAULT_LOGIN,
    password: DEFAULT_PASSWORD
  };

  let answers = defaults;

  if (!process.argv.includes('--yes')) {
    prompt.start();
    prompt.message = '';

    answers = await prompt.get({
      properties: {
        url: { description: 'Site URL (https)', default: defaults.url, required: true },
        login: { description: 'Backoffice user email', default: defaults.login, required: true },
        password: { description: 'Backoffice user password', default: defaults.password, required: true }
      }
    });
  }

  const contents = [
    `UMBRACO_USER_LOGIN=${answers.login}`,
    `UMBRACO_USER_PASSWORD=${answers.password}`,
    `URL=${answers.url.replace(/\/+$/, '')}`,
    `STORAGE_STATE_PATH=${path.join(__dirname, 'playwright', '.auth', 'user.json')}`,
    ''
  ].join('\n');

  fs.writeFileSync(path.join(__dirname, '.env'), contents);
  console.log('\nWrote .env');
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
