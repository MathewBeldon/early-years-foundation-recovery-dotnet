import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(import.meta.dirname, '../..');

function walk(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const fullPath = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

const inputs = [
  'Gemfile.lock',
  'config/routes.rb',
  ...walk(path.join(root, 'app/controllers'))
    .filter(file => file.endsWith('.rb'))
    .map(file => path.relative(root, file).replaceAll('\\', '/')),
  ...walk(path.join(root, 'app/models'))
    .filter(file => file.endsWith('.rb'))
    .map(file => path.relative(root, file).replaceAll('\\', '/')),
  ...walk(path.join(root, 'spec'))
    .filter(file => file.endsWith('_spec.rb'))
    .map(file => path.relative(root, file).replaceAll('\\', '/')),
].sort();

const hash = crypto.createHash('sha256');
for (const relativePath of inputs) {
  hash.update(relativePath);
  hash.update('\0');
  hash.update(fs.readFileSync(path.join(root, relativePath)));
  hash.update('\0');
}

const manifestPath = path.join(root, 'migration/route-manifest.json');
if (!fs.existsSync(manifestPath)) {
  throw new Error('migration/route-manifest.json is missing.');
}

const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const actual = hash.digest('hex');
if (manifest.generatedInEnvironment !== 'test') {
  throw new Error(
    `Route manifest must be generated in Rails test; received ${JSON.stringify(manifest.generatedInEnvironment)}.`,
  );
}
if (manifest.sourceDigest !== actual) {
  throw new Error(
    'migration/route-manifest.json is stale. Run bin/migration-manifest in Git Bash.',
  );
}

if (manifest.routeCount !== manifest.routes.length) {
  throw new Error('routeCount does not match the generated route records.');
}

process.stdout.write(`Route manifest is current (${manifest.routeCount} routes).\n`);
