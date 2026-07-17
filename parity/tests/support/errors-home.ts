import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath, visitHome } from './static-pages.js';

/** Contentful-backed error pages can be slow under migration Compose. */
const PAGE_TIMEOUT_MS = 30_000;

export { visitHome };

export async function visitInternalServerError(
  page: Page,
  baseUrl: string,
): Promise<number | null> {
  const response = await page.goto(`${baseUrl}/500`);
  return response?.status() ?? null;
}

export async function visitServiceUnavailable(
  page: Page,
  baseUrl: string,
): Promise<number | null> {
  const response = await page.goto(`${baseUrl}/503`);
  return response?.status() ?? null;
}

export async function expectInternalServerErrorPage(
  page: Page,
  status: number | null,
): Promise<void> {
  expect(status).toBe(500);
  await expectPath(page, '/500');
  await expect(
    page.getByRole('heading', {
      name: 'Sorry, there is a problem with the service',
    }),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });
  await expect(
    page.getByText('Sorry, there is a problem with the service'),
  ).toBeVisible();
}

export async function expectServiceUnavailablePage(
  page: Page,
  status: number | null,
): Promise<void> {
  expect(status).toBe(503);
  await expectPath(page, '/503');
  await expect(
    page.getByRole('heading', { name: /Service [Uu]navailable/ }),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });
  await expect(page.getByText(/Service [Uu]navailable/)).toBeVisible();
}

/**
 * Signed-in registered user home variant — shorter hero CTA, no anonymous sign-in prompts.
 * Source of truth: spec/system/front_page_spec.rb (authenticated context).
 */
export async function expectAuthenticatedHomePage(page: Page): Promise<void> {
  await expectPath(page, '/');

  await expect(
    page.getByRole('heading', {
      level: 1,
      name: 'Early years child development training',
    }),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });

  await expect(
    page.getByRole('link', { name: 'Learn more', exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Learn more about this training' }),
  ).toHaveCount(0);
  await expect(page.getByRole('link', { name: /Register or sign in/ })).toHaveCount(0);
  await expect(page.getByText('Start your training now')).toHaveCount(0);
  await expect(page.getByText('Access to this website is changing')).toHaveCount(0);
  await expect(
    page.locator('a[href="https://www.gov.uk/using-your-gov-uk-one-login"]'),
  ).toHaveCount(0);
}
