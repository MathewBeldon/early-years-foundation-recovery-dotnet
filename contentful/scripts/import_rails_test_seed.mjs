import { readFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertSafeDestination } from './import_safety.mjs';

const require = createRequire(import.meta.url);
const contentfulImport = require('contentful-import');

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const contentFile = join(root, 'generated', 'rails-test-import.json');
const assetsDirectory = join(root, 'assets');

function required(name) {
  const value = process.env[name]?.trim();
  if (!value) throw new Error(`${name} is required.`);
  return value;
}

const spaceId = required('CONTENTFUL_SPACE');
const environmentId = required('CONTENTFUL_ENVIRONMENT');
const managementToken = required('CONTENTFUL_MANAGEMENT_TOKEN');

const importDocument = JSON.parse(await readFile(contentFile, 'utf8'));
const sourceSpace = importDocument.contentTypes?.[0]?.sys?.space?.sys?.id;
assertSafeDestination({
  allowTestSeed: process.env.CONTENTFUL_ALLOW_TEST_SEED,
  environmentId,
  sourceSpace,
  spaceId,
});

const result = await contentfulImport({
  spaceId,
  environmentId,
  managementToken,
  contentFile,
  uploadAssets: true,
  assetsDirectory,
  skipContentPublishing: false,
  skipContentUpdates: false,
  skipAssetUpdates: false,
  timeout: 30_000,
  retryLimit: 10,
});

const errors = Array.isArray(result?.errors) ? result.errors : [];
if (errors.length > 0) {
  throw new Error(`Contentful import completed with ${errors.length} error(s).`);
}

console.log('Contentful schema and synthetic Rails test content imported successfully.');
