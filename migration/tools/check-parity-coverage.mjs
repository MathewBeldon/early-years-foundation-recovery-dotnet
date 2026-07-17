import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const coveragePath = path.join(root, 'migration/parity-coverage.json');
const coverage = JSON.parse(fs.readFileSync(coveragePath, 'utf8'));
const errors = [];
const validStatuses = new Set(['covered', 'partial', 'excluded']);

function repositoryFileExists(relativePath) {
  const ordinaryPath = path.join(root, relativePath);
  if (fs.existsSync(ordinaryPath)) return true;

  // Compose mounts parity itself at /workspace and the repository evidence at
  // /migration + /spec so the browser container cannot mutate source evidence.
  if (relativePath.startsWith('parity/')) {
    return fs.existsSync(path.join('/workspace', relativePath.slice('parity/'.length)));
  }
  return false;
}

if (coverage.schemaVersion !== 1 || !Array.isArray(coverage.entries)) {
  throw new Error('migration/parity-coverage.json must use schemaVersion 1 and contain entries.');
}

const sourceSpecs = fs
  .readdirSync(path.join(root, 'spec/system'))
  .filter((name) => name.endsWith('_spec.rb'))
  .map((name) => `spec/system/${name}`)
  .sort();
const entriesBySource = new Map();

for (const entry of coverage.entries) {
  if (!entry || typeof entry.source !== 'string') {
    errors.push('Every coverage entry must name a source spec.');
    continue;
  }
  if (entriesBySource.has(entry.source)) {
    errors.push(`Duplicate coverage entry for ${entry.source}.`);
    continue;
  }
  entriesBySource.set(entry.source, entry);

  if (!validStatuses.has(entry.status)) {
    errors.push(`${entry.source} has invalid status ${JSON.stringify(entry.status)}.`);
  }
  if (!Array.isArray(entry.journeys) || !Array.isArray(entry.exclusions)) {
    errors.push(`${entry.source} must contain journey and exclusion arrays.`);
    continue;
  }
  if (entry.status === 'covered' && (entry.journeys.length === 0 || entry.exclusions.length > 0)) {
    errors.push(`${entry.source} is covered but lacks journeys or contains exclusions.`);
  }
  if (entry.status === 'partial' && (entry.journeys.length === 0 || entry.exclusions.length === 0)) {
    errors.push(`${entry.source} is partial without both journeys and explicit exclusions.`);
  }
  if (entry.status === 'excluded' && (entry.journeys.length > 0 || entry.exclusions.length === 0)) {
    errors.push(`${entry.source} is excluded but has journeys or no explicit exclusion.`);
  }

  for (const journey of entry.journeys) {
    if (typeof journey !== 'string' || !journey.startsWith('parity/tests/journeys/')) {
      errors.push(`${entry.source} has invalid journey reference ${JSON.stringify(journey)}.`);
    } else if (!repositoryFileExists(journey)) {
      errors.push(`${entry.source} references missing journey ${journey}.`);
    }
  }
  for (const exclusion of entry.exclusions) {
    for (const field of ['behaviour', 'reason', 'requiredBefore']) {
      if (typeof exclusion?.[field] !== 'string' || !exclusion[field].trim()) {
        errors.push(`${entry.source} has an exclusion without ${field}.`);
      }
    }
  }
}

for (const source of sourceSpecs) {
  if (!entriesBySource.has(source)) errors.push(`Missing parity decision for ${source}.`);
}
for (const source of entriesBySource.keys()) {
  if (!sourceSpecs.includes(source)) errors.push(`Coverage references unknown system spec ${source}.`);
}

if (errors.length > 0) {
  throw new Error(`Parity coverage check failed:\n- ${errors.join('\n- ')}`);
}

const counts = Object.fromEntries(
  [...validStatuses].map((status) => [
    status,
    coverage.entries.filter((entry) => entry.status === status).length,
  ]),
);
process.stdout.write(
  `Parity coverage is explicit for ${sourceSpecs.length} Rails system specs ` +
    `(${counts.covered} covered, ${counts.partial} partial, ${counts.excluded} excluded).\n`,
);
