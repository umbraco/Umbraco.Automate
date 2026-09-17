// Writes .env for the acceptance test suite.
//
// The demo site binds a dynamic port and publishes its address on a named pipe (Windows) or
// unix socket (macOS/Linux), so there is no fixed URL to hard-code. This script asks the
// running site where it is, and only falls back to prompting if it cannot reach one.
//
// Run: npm run config
//   Optional: --pipe <identifier> to target a demo site started from a different branch or
//   worktree (the identifier is the branch name, or the worktree folder name).
//   Optional: --yes to accept every default without prompting (needs a reachable demo site).

const fs = require('fs');
const http = require('http');
const path = require('path');
const { execSync } = require('child_process');
const prompt = require('prompt');

const DEFAULT_LOGIN = 'admin@example.com';
const DEFAULT_PASSWORD = 'password1234';

function sanitize(name) {
  return name.replace(/[^a-zA-Z0-9\-_.]/g, '') || 'default';
}

// Mirrors scripts/test-site-address.js: the demo site names its pipe after the worktree folder
// when running in a worktree, and after the branch otherwise.
function getUniqueIdentifier() {
  const argIndex = process.argv.indexOf('--pipe');
  if (argIndex !== -1 && process.argv[argIndex + 1]) {
    return sanitize(process.argv[argIndex + 1]);
  }

  try {
    const gitDir = execSync('git rev-parse --git-dir', { encoding: 'utf-8' }).trim();

    if (gitDir.includes('worktrees')) {
      const parts = gitDir.split(/[\\/]/);
      const worktreeIndex = parts.findIndex((p) => p === 'worktrees');
      if (worktreeIndex >= 0 && worktreeIndex + 1 < parts.length) {
        return sanitize(parts[worktreeIndex + 1]);
      }
    }

    const branch = execSync('git branch --show-current', { encoding: 'utf-8' }).trim();
    return sanitize(branch || 'default');
  } catch {
    return 'default';
  }
}

function resolveSiteAddress(identifier) {
  const pipeName = `umbraco.demosite.${identifier}`;
  const socketPath =
    process.platform === 'win32' ? `\\\\.\\pipe\\${pipeName}` : `/tmp/${pipeName}`;

  return new Promise((resolve) => {
    const request = http.get({ socketPath, path: '/site-address' }, (res) => {
      let data = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => (data += chunk));
      res.on('end', () => {
        resolve(res.statusCode === 200 ? data.trim() : null);
      });
    });
    request.on('error', () => resolve(null));
    request.setTimeout(3000, () => {
      request.destroy();
      resolve(null);
    });
  });
}

async function main() {
  const identifier = getUniqueIdentifier();
  console.log(`Looking for a running demo site on pipe: umbraco.demosite.${identifier}`);

  // The site reports http and https bindings; the backoffice needs https, because OpenIddict
  // rejects the plain-http one.
  const rawAddress = await resolveSiteAddress(identifier);
  let url = null;
  if (rawAddress) {
    url = rawAddress
      .split(/[\s,;]+/)
      .filter(Boolean)
      .find((candidate) => candidate.startsWith('https://'));
    if (url) {
      // A trailing slash breaks cookie matching in some helpers, so strip it.
      url = url.replace(/\/+$/, '');
      console.log(`Found demo site at ${url}`);
    }
  }

  const defaults = {
    url: url || 'https://localhost:44380',
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
