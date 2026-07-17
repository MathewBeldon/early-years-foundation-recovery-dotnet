import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const root = path.resolve(import.meta.dirname, '..', '..');
const manifest = JSON.parse(
  fs.readFileSync(path.join(root, 'migration', 'route-manifest.json'), 'utf8'),
);
const evidence = JSON.parse(
  fs.readFileSync(path.join(root, 'migration', 'acceptance-evidence.json'), 'utf8'),
);

const requiredGates = [
  'inventory',
  'contract',
  'browser-ui',
  'accessibility',
  'data',
  'side-effects',
  'security',
  'operations',
  'boundary',
  'review',
];
const routesById = new Map(manifest.routes.map((route) => [route.id, route]));
const errors = [];

if (evidence.schemaVersion !== 1 || typeof evidence.routes !== 'object' || !evidence.routes) {
  errors.push('acceptance-evidence.json must use schemaVersion 1 and contain a routes object.');
}

for (const routeId of Object.keys(evidence.routes ?? {})) {
  if (!routesById.has(routeId)) {
    errors.push(`Acceptance evidence references unknown route ${routeId}.`);
  }
}

for (const route of manifest.routes) {
  if (route.migrationState !== 'approved-dotnet') continue;

  const routeEvidence = evidence.routes[route.id];
  if (!routeEvidence || typeof routeEvidence.gates !== 'object') {
    errors.push(`Approved route ${route.id} (${route.path}) has no acceptance evidence.`);
    continue;
  }

  for (const gateName of requiredGates) {
    const gate = routeEvidence.gates[gateName];
    if (!gate || typeof gate.applicable !== 'boolean') {
      errors.push(`Approved route ${route.id} has no decision for gate ${gateName}.`);
      continue;
    }

    if (gate.applicable) {
      if (
        !Array.isArray(gate.evidence) ||
        gate.evidence.length === 0 ||
        gate.evidence.some((reference) => typeof reference !== 'string' || !reference.trim())
      ) {
        errors.push(`Approved route ${route.id} gate ${gateName} has no evidence.`);
      }
    } else if (typeof gate.justification !== 'string' || !gate.justification.trim()) {
      errors.push(
        `Approved route ${route.id} gate ${gateName} is not applicable without justification.`,
      );
    }
  }
}

if (errors.length > 0) {
  process.stderr.write(`${errors.join('\n')}\n`);
  process.exitCode = 1;
} else {
  process.stdout.write('Migration acceptance evidence is structurally valid.\n');
}
