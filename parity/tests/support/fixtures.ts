import fs from 'node:fs';
import type { BrowserContext, Page } from '@playwright/test';
import { test as base } from '@playwright/test';
import { targetUrls, type TargetUrls } from './environment.js';
import { guardJourneyOrigins } from './origin-guard.js';

interface ParityFixtures {
  urls: TargetUrls;
  anonymousContext: BrowserContext;
  authenticatedContext: BrowserContext | null;
  targetPage: Page;
  railsPage: Page;
  dotnetPage: Page;
  gatewayPage: Page;
}

const deterministicContext = {
  viewport: { width: 1280, height: 720 },
  locale: 'en-GB',
  timezoneId: 'Europe/London',
  colorScheme: 'light' as const,
  reducedMotion: 'reduce' as const,
  serviceWorkers: 'block' as const,
};

export const test = base.extend<ParityFixtures>({
  urls: async ({}, use) => use(targetUrls()),

  anonymousContext: async ({ browser }, use) => {
    const context = await browser.newContext(deterministicContext);
    await use(context);
    await context.close();
  },

  authenticatedContext: async ({ browser }, use) => {
    const storageState = process.env.AUTHENTICATED_STORAGE_STATE;
    if (!storageState || !fs.existsSync(storageState)) {
      await use(null);
      return;
    }

    const context = await browser.newContext({
      ...deterministicContext,
      storageState,
    });
    await use(context);
    await context.close();
  },

  targetPage: async ({ browser, urls }, use) => {
    const context = await browser.newContext(deterministicContext);
    const page = await context.newPage();
    const assertNoOriginEscape = guardJourneyOrigins(page, urls);
    try {
      await use(page);
      assertNoOriginEscape();
    } finally {
      await context.close();
    }
  },

  railsPage: async ({ anonymousContext }, use) => {
    const page = await anonymousContext.newPage();
    await use(page);
  },

  dotnetPage: async ({ browser }, use) => {
    const context = await browser.newContext(deterministicContext);
    const page = await context.newPage();
    await use(page);
    await context.close();
  },

  gatewayPage: async ({ browser }, use) => {
    const context = await browser.newContext(deterministicContext);
    const page = await context.newPage();
    await use(page);
    await context.close();
  },
});

export { expect } from '@playwright/test';

export function requireAuthenticatedContext(
  context: BrowserContext | null,
): BrowserContext {
  if (!context) {
    throw new Error(
      'Authenticated parity is not configured. Set AUTHENTICATED_STORAGE_STATE to an approved simulator-generated state file.',
    );
  }

  return context;
}
