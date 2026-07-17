import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath } from './static-pages.js';

/** Contentful-backed public pages can be slow under migration Compose. */
const PAGE_TIMEOUT_MS = 30_000;

export interface GuestStaticPage {
  path: string;
  heading: string;
}

/** Guest static routes from spec/system/page_title_spec.rb and spec/requests/static_spec.rb. */
export const GUEST_STATIC_PAGES: GuestStaticPage[] = [
  { path: '/sitemap', heading: 'Sitemap' },
  { path: '/terms-and-conditions', heading: 'Terms and conditions' },
  { path: '/promotional-materials', heading: 'Promotional materials' },
  { path: '/wifi-and-data', heading: 'Free internet, wifi and data resources' },
  { path: '/new-registration', heading: 'Update your registration details' },
  { path: '/other-problems-signing-in', heading: 'Other problems signing in' },
];

export const WHATS_NEW_PATH = '/whats-new';
export const WHATS_NEW_HEADING = "What's new in the training";

export async function visitAboutAlpha(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/about/alpha`);
}

/** Mirrors spec/system/about_spec.rb — first module about hero. */
export async function expectAboutAlphaHero(page: Page): Promise<void> {
  await expectPath(page, '/about/alpha');

  const hero = page.locator('#hero-layout');
  await expect(
    hero.getByRole('heading', { name: /First Training Module/ }).first(),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });
  await expect(hero.getByText('first module description').first()).toBeVisible();
}

export async function visitGuestStaticPage(
  page: Page,
  baseUrl: string,
  path: string,
): Promise<number | null> {
  const response = await page.goto(`${baseUrl}${path}`);
  return response?.status() ?? null;
}

/** Exact public-content contract: 200, title/path, named h1, and seeded body. */
export async function expectGuestStaticPageLoaded(
  page: Page,
  path: string,
  heading: string,
  status: number | null,
): Promise<void> {
  expect(status).toBe(200);
  await expectPath(page, path);
  await expectNotServerErrorPage(page);

  const escapedHeading = heading.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  await expect(page).toHaveTitle(new RegExp(escapedHeading));
  const h1 = page.getByRole('heading', { level: 1, name: heading, exact: true });
  await expect(h1.first()).toBeVisible({ timeout: PAGE_TIMEOUT_MS });
  const main = page.locator('main');
  await expect(main).toContainText(heading);
  await expect(main).toContainText('Synthetic content for automated tests.');
}

export async function visitWhatsNew(
  page: Page,
  baseUrl: string,
): Promise<number | null> {
  const response = await page.goto(`${baseUrl}${WHATS_NEW_PATH}`);
  return response?.status() ?? null;
}

export async function expectNotServerErrorPage(page: Page): Promise<void> {
  await expect(
    page.getByRole('heading', { name: 'Sorry, there is a problem with the service' }),
  ).toHaveCount(0);
}

/** Auth-gated static page — mirrors page_title_spec authenticated whats-new title. */
export async function expectWhatsNewPage(
  page: Page,
  status: number | null,
): Promise<void> {
  expect(status).toBe(200);
  await expectPath(page, WHATS_NEW_PATH);
  await expectNotServerErrorPage(page);
  await expect(
    page.getByRole('heading', { level: 1, name: WHATS_NEW_HEADING }),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });
  await expect(page.getByText('Synthetic content for automated tests.')).toBeVisible();
}
