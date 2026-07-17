import type { BrowserContext, Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath } from './registration.js';

export const ANALYTICS_COOKIE_NAME = 'track_analytics_v2';

export const COOKIE_BANNER_HEADING =
  'Cookies on Early years child development training';

const STEP_TIMEOUT_MS = 15_000;

export async function getAnalyticsCookieValue(
  context: BrowserContext,
  baseUrl: string,
): Promise<string | undefined> {
  const cookies = await context.cookies(baseUrl);
  return cookies.find((cookie) => cookie.name === ANALYTICS_COOKIE_NAME)?.value;
}

export async function setAnalyticsCookie(
  page: Page,
  baseUrl: string,
  value: string,
): Promise<void> {
  const host = new URL(baseUrl).hostname;
  await page.context().addCookies([
    {
      name: ANALYTICS_COOKIE_NAME,
      value,
      domain: host,
      path: '/',
      httpOnly: true,
    },
  ]);
}

/**
 * Fresh browser context has no consent cookie; home shows the banner.
 */
export async function visitHomeAsFreshVisitor(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/`);
  await page.waitForLoadState('domcontentloaded');
}

export async function expectCookieBanner(page: Page): Promise<void> {
  await expect(page.getByText(COOKIE_BANNER_HEADING)).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function expectCookieBannerHidden(page: Page): Promise<void> {
  await expect(page.getByText(COOKIE_BANNER_HEADING)).not.toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function acceptAnalyticsCookiesFromBanner(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Accept analytics cookies' }).click();
  await expectAnalyticsCookie(page, 'true');
}

export async function rejectAnalyticsCookiesFromBanner(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Reject analytics cookies' }).click();
  await expectAnalyticsCookie(page, 'false');
}

async function expectAnalyticsCookie(page: Page, expectedValue: string): Promise<void> {
  const baseUrl = new URL(page.url()).origin;
  await expect
    .poll(() => getAnalyticsCookieValue(page.context(), baseUrl), {
      timeout: STEP_TIMEOUT_MS,
      message: `analytics consent cookie should become ${expectedValue}`,
    })
    .toBe(expectedValue);
}

export async function openCookiePolicyFromBanner(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Read the cookie policy' }).click();
  await expectPath(page, '/settings/cookie-policy');
}

export async function visitCookiePolicy(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/settings/cookie-policy`);
  await page.waitForLoadState('domcontentloaded');
}

export async function expectCookiePolicyPage(page: Page): Promise<void> {
  await expectPath(page, '/settings/cookie-policy');
  await expect(page.locator('#cookies')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function saveCookiePreference(
  page: Page,
  accept: boolean,
): Promise<void> {
  const choice = accept ? 'Yes' : 'No';
  await page.getByRole('radio', { name: choice, exact: true }).check();
  await page.getByRole('button', { name: 'Save cookie settings' }).click();
  await expectAnalyticsCookie(page, String(accept));
}
