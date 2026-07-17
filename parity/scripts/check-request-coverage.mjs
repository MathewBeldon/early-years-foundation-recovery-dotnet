import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const hostPath = path.resolve('../migration/tools/check-request-coverage.mjs');
const checkerPath = fs.existsSync(hostPath)
  ? hostPath
  : '/migration/tools/check-request-coverage.mjs';

if (!fs.existsSync(checkerPath)) {
  throw new Error('Request coverage checker is not mounted or present in the repository.');
}

await import(pathToFileURL(checkerPath).href);
