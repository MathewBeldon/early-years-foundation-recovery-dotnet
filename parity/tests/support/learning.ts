import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import type { SimulatorUser } from './auth.js';
import {
  chooseSettingByLabelAndContinue,
  chooseWhereYouLiveAndContinue,
  completePreferences,
  confirmCheckYourAnswers,
  expectPath,
  startFreshRegistration,
} from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

/**
 * Shortest Contentful-backed registration: England + DfE skips LA/role/experience.
 * Lands on /my-modules for a newly registered user.
 */
export async function registerMinimalUser(
  page: Page,
  baseUrl: string,
  options: { label: string; firstName?: string; surname?: string } = {
    label: 'learner',
  },
): Promise<SimulatorUser> {
  const identity = await startFreshRegistration(page, baseUrl, {
    label: options.label,
    firstName: options.firstName ?? 'Lee',
    surname: options.surname ?? 'Learner',
  });
  await chooseWhereYouLiveAndContinue(page, 'England');
  await chooseSettingByLabelAndContinue(page, 'Department for Education');
  await completePreferences(page, { emails: 'no', research: 'No' });
  await confirmCheckYourAnswers(page);
  await expectPath(page, '/my-modules');
  return identity;
}

export async function expectMyModulesPage(page: Page): Promise<void> {
  await expectPath(page, '/my-modules');
  await expect(page.getByRole('heading', { name: 'My modules' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function expectEmptyInProgress(page: Page): Promise<void> {
  const started = page.locator('#started');
  await expect(started.getByText('Modules in progress')).toBeVisible();
  await expect(started.getByText('You have not started any modules.')).toBeVisible();
  await expect(started.getByText('First Training Module')).toHaveCount(0);
}

export async function expectAvailableModules(
  page: Page,
  titles: string[],
): Promise<void> {
  const available = page.locator('#available');
  await expect(available.getByText('Available modules')).toBeVisible();
  for (const title of titles) {
    await expect(available.getByText(title)).toBeVisible();
  }
  await expect(available.getByText('Fourth Training Module')).toHaveCount(0);
}

export async function expectUpcomingModule(page: Page, title: string): Promise<void> {
  const upcoming = page.locator('#upcoming');
  await expect(upcoming.getByText('Future modules in this course')).toBeVisible();
  await expect(upcoming.getByText(title)).toBeVisible();
}

export async function openModuleFromCard(
  page: Page,
  titlePattern: RegExp,
): Promise<void> {
  const moduleLink = page.getByRole('link', { name: titlePattern }).first();
  await expect(moduleLink).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await Promise.all([
    page.waitForURL(/\/modules\/alpha(?:\?.*)?$/, { timeout: STEP_TIMEOUT_MS }),
    moduleLink.click(),
  ]);
  await expectPath(page, '/modules/alpha');
}

export async function expectModuleOverview(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha');
  await expect(
    page.getByRole('link', { name: 'Back to My modules' }),
  ).toBeVisible();
  await expect(page.getByText('Module 1: First Training Module')).toBeVisible();
  await expect(page.getByText('first module description')).toBeVisible();
  await expect(page.getByText('The first submodule')).toBeVisible();
  await expect(page.getByText('The second submodule')).toBeVisible();
  await expect(page.getByText('Summary and next steps')).toBeVisible();
  // Overview renders Start/Resume CTAs at top and bottom.
  await expect(page.getByRole('link', { name: 'Start module' }).first()).toBeVisible();
}

export async function startModule(page: Page): Promise<void> {
  await page.getByRole('link', { name: 'Start module' }).first().click();
  await expectPath(page, '/modules/alpha/content-pages/what-to-expect');
  await expect(
    page.getByRole('heading', { name: 'What to expect during the training' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function continueFromInterruption(page: Page): Promise<void> {
  await page.locator('#next-action').click();
  await expectPath(page, '/modules/alpha/content-pages/1-1');
  await expect(
    page.getByRole('heading', { name: 'The first submodule' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function expectInProgressModule(
  page: Page,
  title: string,
): Promise<void> {
  await expectMyModulesPage(page);
  const started = page.locator('#started');
  await expect(started.getByText('Modules in progress')).toBeVisible();
  await expect(started.getByText(title)).toBeVisible();
  await expect(started.getByText('You have not started any modules.')).toHaveCount(0);
  await expect(started.getByText(/Your progress:/)).toBeVisible();
}
