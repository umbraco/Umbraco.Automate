// Runs config.js after `npm install`, but only when there is no .env yet, so an existing
// developer setup is never overwritten by a routine install.

const fs = require('fs');
const path = require('path');

if (fs.existsSync(path.join(__dirname, '.env'))) {
  console.log('.env already exists — skipping config. Run "npm run config" to change it.');
  process.exit(0);
}

if (process.env.CI) {
  console.log('CI detected — skipping interactive config.');
  process.exit(0);
}

require('./config.js');
