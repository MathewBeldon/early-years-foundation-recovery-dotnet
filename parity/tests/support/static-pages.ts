import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

/** Contentful-backed public pages can be slow under migration Compose. */
const PAGE_TIMEOUT_MS = 30_000;

export async function expectPath(page: Page, path: string): Promise<void> {
  const escaped = path.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  await expect(page).toHaveURL(new RegExp(`${escaped}(?:\\?.*)?$`), {
    timeout: PAGE_TIMEOUT_MS,
  });
}

export async function visitHome(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/`);
}

export async function expectAnonymousHomePage(page: Page): Promise<void> {
  await expect(
    page.getByRole('heading', {
      level: 1,
      name: 'Early years child development training',
    }),
  ).toBeVisible({ timeout: PAGE_TIMEOUT_MS });

  await expect(page.getByText('Learn more about this training')).toBeVisible();
  await expect(
    page.getByRole('link', { name: 'Learn more about this training' }),
  ).toBeVisible();
  await expect(page.getByRole('link', { name: /Register or sign in/ })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Who this training is for' })).toBeVisible();
}

export async function followLearnMoreAboutTraining(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Learn more about this training' }).click();
}

export async function followRegisterOrSignIn(page: Page): Promise<void> {
  await page.getByRole('link', { name: /Register or sign in/ }).click();
}

export async function expectCourseOverviewPage(page: Page): Promise<void> {
  await expectPath(page, '/about-training');

  const hero = page.locator('#hero-layout');
  await expect(
    hero.getByRole('heading', { name: 'About this training course' }),
  ).toBeVisible();
  await expect(
    hero.getByText('The course has 4 modules. 3 modules are currently available.'),
  ).toBeVisible();
}

export async function expectExpertsPage(page: Page): Promise<void> {
  await expectPath(page, '/about/the-experts');

  const hero = page.locator('#hero-layout');
  await expect(hero.getByRole('heading', { name: 'The experts' })).toBeVisible();
  await expect(
    hero.getByText('This training course has been created by early years experts.'),
  ).toBeVisible();
}

export async function expectAccessibilityStatementPage(page: Page): Promise<void> {
  await expectPath(page, '/accessibility-statement');
  await expect(page.locator('#accessibility-statement')).toBeVisible();
}

export async function expectNotFoundPage(page: Page): Promise<void> {
  await expectPath(page, '/404');
  await expect(page.getByRole('heading', { name: 'Page not found' })).toBeVisible();
  await expect(
    page.getByText('If you typed the web address, check it is correct.'),
  ).toBeVisible();
}
