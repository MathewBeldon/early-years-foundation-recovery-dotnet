import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const BRAVO_CERTIFICATE_PATH = '/modules/bravo/content-pages/1-3-4';

export const DEFAULT_CERTIFICATE_USER = {
  firstName: 'Lee',
  surname: 'Learner',
} as const;

export async function visitCertificatePage(
  page: Page,
  baseUrl: string,
  moduleName: 'alpha' | 'bravo',
): Promise<void> {
  await page.goto(`${baseUrl}/modules/${moduleName}/content-pages/1-3-4`);
  await expectPath(page, `/modules/${moduleName}/content-pages/1-3-4`);
}

export async function expectIncompleteCertificateGuard(page: Page): Promise<void> {
  await expect(page.getByText('You have not yet completed the module.')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.getByText('Date completed:')).toHaveCount(0);
  await expect(page.getByText('Your name will appear here')).toBeVisible();
}

export async function expectCertificateOmitsUserName(
  page: Page,
  firstName: string,
  surname: string,
): Promise<void> {
  await expect(page.getByText(firstName, { exact: true })).toHaveCount(0);
  await expect(page.getByText(surname, { exact: true })).toHaveCount(0);
}

export async function expectCompleteCertificateWithName(
  page: Page,
  options: { firstName: string; surname: string },
): Promise<void> {
  await expect(
    page.getByText('Congratulations! You have now completed this module.'),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(page.locator('h1[data-clarity-mask="True"]')).toContainText(
    `${options.firstName} ${options.surname}`,
  );
  await expect(page.getByText(options.firstName)).toBeVisible();
  await expect(page.getByText(options.surname)).toBeVisible();
}

/** Assert completion date when the live completion path records one. */
export async function expectCertificateCompletionDateIfPresent(
  page: Page,
): Promise<void> {
  const dateLine = page.locator('#certificate-date');
  if ((await dateLine.count()) > 0) {
    await expect(dateLine).toContainText('Date completed:');
  }
}
