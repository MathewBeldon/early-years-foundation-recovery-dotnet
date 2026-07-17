import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { targetUrls } from './environment.js';
import { allowedJourneyOrigins } from './origin-guard.js';

function fileDigest(relativePath: string): string {
  return crypto
    .createHash('sha256')
    .update(fs.readFileSync(path.resolve(relativePath)))
    .digest('hex');
}

function recorded(name: string, fallback: string | null): string | null {
  const value = process.env[name]?.trim();
  return value || fallback;
}

export default function globalSetup(): void {
  const urls = targetUrls();
  const githubActions = process.env.GITHUB_ACTIONS === 'true';
  const runKind = process.env.PARITY_RUN_KIND ?? 'unspecified';
  const fullyParallel = ['rails-direct', 'gateway'].includes(runKind);
  const requiredProvenance = [
    'SOURCE_COMMIT',
    'CI_RUN_ID',
    'CI_RUN_ATTEMPT',
    'CONTENTFUL_FIXTURE_DIGEST',
    'CONTENTFUL_REMOTE_DIGEST',
  ];

  if (githubActions) {
    const missing = requiredProvenance.filter((name) => !process.env[name]);
    if (missing.length > 0) {
      throw new Error(`GitHub Actions parity provenance is incomplete: ${missing.join(', ')}.`);
    }
  }

  const metadata = {
    schemaVersion: 1,
    generatedAt: new Date().toISOString(),
    runKind,
    target: {
      url: urls.target,
      origin: new URL(urls.target).origin,
      allowedJourneyOrigins: [...allowedJourneyOrigins(urls)].sort(),
    },
    source: {
      commit: recorded('SOURCE_COMMIT', 'local-unrecorded'),
      ciRunId: recorded('CI_RUN_ID', null),
      ciRunAttempt: recorded('CI_RUN_ATTEMPT', null),
    },
    fixtures: {
      canonicalDatabase: fileDigest('fixtures/canonical.sql'),
      databaseCapturePlan: fileDigest('fixtures/database-capture-plan.json'),
      parityPackageLock: fileDigest('package-lock.json'),
      contentfulLocal: recorded('CONTENTFUL_FIXTURE_DIGEST', 'local-unrecorded'),
      contentfulRemote: recorded('CONTENTFUL_REMOTE_DIGEST', 'local-unrecorded'),
      contentfulEnvironment: recorded('CONTENTFUL_TEST_ENVIRONMENT', null),
    },
    runtime: {
      node: process.version,
      playwright: JSON.parse(fs.readFileSync('package.json', 'utf8')).devDependencies[
        '@playwright/test'
      ],
      workers: process.env.PLAYWRIGHT_WORKERS ?? (fullyParallel ? '6' : '1'),
      fullyParallel,
      railsReferenceEnvironment: recorded('RAILS_REFERENCE_ENVIRONMENT', null),
      retries: process.env.CI === 'true' ? 1 : 0,
      failOnFlakyTests: process.env.CI === 'true',
    },
  };

  fs.mkdirSync('reports', { recursive: true });
  fs.writeFileSync('reports/run-metadata.json', `${JSON.stringify(metadata, null, 2)}\n`);
}
