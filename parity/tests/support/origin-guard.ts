import type { Page } from '@playwright/test';
import type { TargetUrls } from './environment.js';

const browserProtocols = new Set(['http:', 'https:']);

export function allowedJourneyOrigins(urls: TargetUrls): ReadonlySet<string> {
  const origins = new Set([new URL(urls.target).origin, new URL(urls.govOneSimulator).origin]);

  // Host-headed runs map this Compose hostname to localhost inside Chromium, but
  // the browser-visible URL (and therefore its origin) remains gov-one-simulator.
  if (process.env.PLAYWRIGHT_MAP_GOV_ONE_SIMULATOR === '1') {
    origins.add('http://gov-one-simulator:4000');
  }

  const configuredBrowserOrigin = process.env.GOV_ONE_BROWSER_ORIGIN;
  if (configuredBrowserOrigin) {
    origins.add(new URL(configuredBrowserOrigin).origin);
  }

  return origins;
}

export function assertAllowedMainFrameUrl(
  rawUrl: string,
  allowedOrigins: ReadonlySet<string>,
): void {
  if (rawUrl === 'about:blank') return;

  const url = new URL(rawUrl);
  if (!browserProtocols.has(url.protocol)) return;
  if (allowedOrigins.has(url.origin)) return;

  throw new Error(
    `Journey escaped its approved origins: ${url.origin}. Allowed origins: ${[
      ...allowedOrigins,
    ].join(', ')}.`,
  );
}

/**
 * Records every main-frame origin violation and fails fixture teardown. This is
 * deliberately independent of path assertions: a gateway journey must never
 * become green after following a redirect directly to Rails.
 */
export function guardJourneyOrigins(page: Page, urls: TargetUrls): () => void {
  const allowedOrigins = allowedJourneyOrigins(urls);
  const violations: Error[] = [];

  page.on('framenavigated', (frame) => {
    if (frame !== page.mainFrame()) return;

    try {
      assertAllowedMainFrameUrl(frame.url(), allowedOrigins);
    } catch (error) {
      violations.push(error instanceof Error ? error : new Error(String(error)));
    }
  });

  return () => {
    if (violations.length > 0) throw violations[0];
  };
}
