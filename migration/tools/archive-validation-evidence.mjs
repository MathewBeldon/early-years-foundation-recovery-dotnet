import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');
const status = process.argv[2];
if (!['green', 'failed'].includes(status)) {
  throw new Error('Usage: node migration/tools/archive-validation-evidence.mjs {green|failed}');
}

const now = new Date();
const parts = Object.fromEntries(
  new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Europe/London',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23',
  })
    .formatToParts(now)
    .filter((part) => part.type !== 'literal')
    .map((part) => [part.type, part.value]),
);
const stamp = `${parts.year}-${parts.month}-${parts.day}-${parts.hour}${parts.minute}${parts.second}`;
const archiveRoot = path.join(root, 'tmp/parity/validation-archives');
const archive = path.join(archiveRoot, `${stamp}-${status}`);
const validationWork = path.join(root, 'tmp/parity/validation-work');
fs.mkdirSync(archive, { recursive: true });

if (fs.existsSync(validationWork)) {
  fs.cpSync(validationWork, path.join(archive, 'evidence'), { recursive: true });
}

const files = [];
function walk(directory) {
  if (!fs.existsSync(directory)) return;
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const absolute = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(absolute);
    else files.push(absolute);
  }
}
walk(archive);

const checksums = files
  .sort()
  .map((absolute) => ({
    path: path.relative(archive, absolute).replaceAll('\\', '/'),
    sha256: crypto.createHash('sha256').update(fs.readFileSync(absolute)).digest('hex'),
  }));
const summary = {
  schemaVersion: 1,
  status,
  startedBy: 'bin/migration-validate',
  archivedAt: now.toISOString(),
  timezone: 'Europe/London',
  sourceCommit: process.env.SOURCE_COMMIT ?? process.env.GITHUB_SHA ?? 'local-unrecorded',
  checksums,
};
fs.writeFileSync(
  path.join(archive, 'archive-manifest.json'),
  `${JSON.stringify(summary, null, 2)}\n`,
);

if (status === 'green') {
  const firstGreen = path.join(archiveRoot, 'first-green.json');
  if (!fs.existsSync(firstGreen)) {
    fs.writeFileSync(
      firstGreen,
      `${JSON.stringify({ archive: path.basename(archive), archivedAt: now.toISOString() }, null, 2)}\n`,
    );
  }
}

process.stdout.write(`${archive}\n`);
