import { defineConfig, devices } from '@playwright/test';

function nonNegativeInteger(name: string, fallback: number): number {
  const raw = process.env[name];
  if (raw === undefined || raw === '') {
    return fallback;
  }

  const parsed = Number(raw);
  if (!Number.isSafeInteger(parsed) || parsed < 0) {
    throw new Error(`${name} must be a non-negative integer; received ${JSON.stringify(raw)}.`);
  }

  return parsed;
}

function positiveInteger(name: string, fallback: number): number {
  const parsed = nonNegativeInteger(name, fallback);
  if (parsed === 0) {
    throw new Error(`${name} must be greater than zero.`);
  }

  return parsed;
}

function enabled(name: string): boolean {
  return ['1', 'true'].includes((process.env[name] ?? '').toLowerCase());
}

const slowMo = nonNegativeInteger('PLAYWRIGHT_SLOW_MO', 0);
const testTimeout = positiveInteger('PLAYWRIGHT_TEST_TIMEOUT', 30_000);
const runKind = process.env.PARITY_RUN_KIND ?? 'unspecified';
const runningJourneys = ['rails-direct', 'gateway'].includes(runKind);
// Interactive One Login identity is bound to each authorize flow. Journey tests use
// unique synthetic users, so six workers can balance large spec files safely. Platform
// and database checks retain a conservative single-worker fallback.
const workers = positiveInteger('PLAYWRIGHT_WORKERS', runningJourneys ? 6 : 1);

// Host headed runs: Rails authorize links use http://gov-one-simulator:4000 while
// the simulator is published on localhost:4000. Map the Compose hostname in Chromium
// so a Windows hosts file entry is not required.
const mapGovOneSimulator = enabled('PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR');
const runningInCi = enabled('CI');

const chromiumArgs = [
  // Netavark provides service names through the container's resolver.
  // Force Chromium to use it instead of the browser's asynchronous DNS client.
  '--disable-features=AsyncDns',
  ...(mapGovOneSimulator
    ? ['--host-resolver-rules=MAP gov-one-simulator 127.0.0.1']
    : []),
];

export default defineConfig({
  testDir: './tests',
  outputDir: './test-results',
  globalSetup: './tests/support/global-setup.ts',
  fullyParallel: runningJourneys,
  forbidOnly: runningInCi,
  failOnFlakyTests: runningInCi,
  retries: runningInCi ? 1 : 0,
  workers,
  timeout: testTimeout,
  expect: {
    timeout: 5_000,
    toHaveScreenshot: {
      animations: 'disabled',
      caret: 'hide',
      maxDiffPixels: 0,
      threshold: 0,
    },
  },
  reporter: [
    ['line'],
    ['html', { outputFolder: 'reports/playwright-report', open: 'never' }],
    ['junit', { outputFile: 'reports/junit.xml' }],
  ],
  snapshotPathTemplate: '{testDir}/../baselines/{projectName}/{testFilePath}/{arg}{ext}',
  use: {
    ...devices['Desktop Chrome'],
    locale: 'en-GB',
    timezoneId: 'Europe/London',
    colorScheme: 'light',
    contextOptions: { reducedMotion: 'reduce' },
    serviceWorkers: 'block',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        launchOptions: {
          args: chromiumArgs,
          ...(slowMo > 0 ? { slowMo } : {}),
        },
      },
    },
  ],
});
