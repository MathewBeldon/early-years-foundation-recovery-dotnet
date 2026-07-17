import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const manifest = JSON.parse(
  fs.readFileSync(path.join(root, 'migration/request-coverage.json'), 'utf8'),
);
const errors = [];
const validStatuses = new Set(['covered', 'source-only', 'excluded']);

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

function repositoryFileExists(relativePath) {
  if (fs.existsSync(path.join(root, relativePath))) return true;
  if (relativePath.startsWith('parity/')) {
    return fs.existsSync(path.join('/workspace', relativePath.slice('parity/'.length)));
  }
  if (relativePath.startsWith('spec/')) {
    return fs.existsSync(path.join('/', relativePath));
  }
  if (relativePath.startsWith('migration/')) {
    return fs.existsSync(path.join('/migration', relativePath.slice('migration/'.length)));
  }
  return false;
}

const requestRoot = fs.existsSync(path.join(root, 'spec/requests'))
  ? path.join(root, 'spec/requests')
  : '/spec/requests';
const sourceSpecs = walk(requestRoot)
  .filter((name) => name.endsWith('_spec.rb'))
  .map((name) => `spec/requests/${path.relative(requestRoot, name).replaceAll('\\', '/')}`)
  .sort();
const entries = new Map();

if (manifest.schemaVersion !== 1 || !Array.isArray(manifest.entries)) {
  throw new Error('migration/request-coverage.json must use schemaVersion 1 and contain entries.');
}

for (const entry of manifest.entries) {
  if (!entry || typeof entry.source !== 'string') {
    errors.push('Every request-coverage entry must name a source spec.');
    continue;
  }
  if (entries.has(entry.source)) errors.push(`Duplicate request coverage for ${entry.source}.`);
  entries.set(entry.source, entry);

  if (!validStatuses.has(entry.status)) {
    errors.push(`${entry.source} has invalid status ${JSON.stringify(entry.status)}.`);
  }
  if (!Array.isArray(entry.behaviours) || entry.behaviours.length === 0) {
    errors.push(`${entry.source} must inventory at least one behaviour.`);
  }
  if (!Array.isArray(entry.evidence)) {
    errors.push(`${entry.source} must contain an evidence array.`);
  } else {
    for (const evidence of entry.evidence) {
      if (typeof evidence !== 'string' || !repositoryFileExists(evidence)) {
        errors.push(`${entry.source} references missing evidence ${JSON.stringify(evidence)}.`);
      }
    }
  }
  if (typeof entry.requiredBefore !== 'string' || !entry.requiredBefore.trim()) {
    errors.push(`${entry.source} must state what is required before migration.`);
  }
  if (entry.status === 'covered' && entry.evidence.length === 0) {
    errors.push(`${entry.source} is covered but has no parity evidence.`);
  }
  if (entry.status === 'source-only' && entry.evidence.length !== 0) {
    errors.push(`${entry.source} is source-only but unexpectedly claims parity evidence.`);
  }
}

for (const source of sourceSpecs) {
  if (!entries.has(source)) errors.push(`Missing request coverage decision for ${source}.`);
}
for (const source of entries.keys()) {
  if (!sourceSpecs.includes(source)) errors.push(`Request coverage references unknown spec ${source}.`);
}

const requiredSecurityBehaviours = new Map([
  ['spec/requests/bot_spec.rb', ['invalid or missing audit token', 'audit token does not grant a user session']],
  ['spec/requests/users/openid_connect_callback_spec.rb', ['provider error or cancellation', 'missing or mismatched state', 'nonce and token validation']],
  ['spec/requests/webhooks_spec.rb', ['unauthenticated denial without writes', 'invalid-attempt throttling and recovery']],
]);
for (const [source, behaviours] of requiredSecurityBehaviours) {
  const entry = entries.get(source);
  for (const behaviour of behaviours) {
    if (!entry?.behaviours?.includes(behaviour)) {
      errors.push(`${source} must explicitly inventory security behaviour: ${behaviour}.`);
    }
  }
}

if (errors.length > 0) {
  throw new Error(`Request coverage check failed:\n- ${errors.join('\n- ')}`);
}

const counts = Object.fromEntries(
  [...validStatuses].map((status) => [
    status,
    manifest.entries.filter((entry) => entry.status === status).length,
  ]),
);
process.stdout.write(
  `Request coverage is explicit for ${sourceSpecs.length} Rails request specs ` +
    `(${counts.covered} covered, ${counts['source-only']} source-only, ${counts.excluded} excluded).\n`,
);
