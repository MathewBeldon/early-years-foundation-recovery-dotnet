import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  continueFromInterruption,
  openModuleFromCard,
  registerMinimalUser,
  startModule,
} from './learning.js';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  clickTrainingAction,
  walkModuleSteps,
  type ModuleStep,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const LEARNING_LOG_PATH = '/my-account/learning-log';
export const ALPHA_NOTE_PAGE_PATH = '/modules/alpha/content-pages/1-1-3-1';

/** Steps from module start up to (not including) the alpha reflection note page. */
const ALPHA_BEFORE_NOTE_PAGE: ModuleStep[] = ALPHA_PASS_SKIP_FEEDBACK.slice(0, 5);

/** Arrive at 1-1-3-1 without saving — caller fills the note form. */
export const ALPHA_NOTE_PAGE_STEP: ModuleStep = {
  path: ALPHA_NOTE_PAGE_PATH,
  text: '1-1-3-1',
  actions: [],
};

export async function visitLearningLog(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}${LEARNING_LOG_PATH}`);
}

export async function expectLearningLogPage(page: Page): Promise<void> {
  await expectPath(page, LEARNING_LOG_PATH);
  await expect(
    page.getByRole('heading', { name: 'Your learning log', level: 1 }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(
    page.getByText('View your notes from all your modules here.'),
  ).toBeVisible();
}

export async function expectEmptyLearningLogModule(
  page: Page,
  moduleLabel = 'Module 1',
): Promise<void> {
  await expectLearningLogPage(page);
  await expect(page.getByRole('tab', { name: moduleLabel })).toBeVisible();
  await expect(
    page.getByText('You have not made any notes for this module.'),
  ).toBeVisible();
}

export async function expectNoteBodyOnLearningLog(
  page: Page,
  body: string,
): Promise<void> {
  await expectLearningLogPage(page);
  const entry = page.locator('.log-entry').filter({ hasText: body });
  await expect(entry.getByRole('heading', { name: 'You wrote:' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(entry.getByText(body, { exact: true })).toBeVisible();
}

/**
 * Start alpha and walk content pages up to the reflection note page (1-1-3-1).
 * Caller must already be registered and on my-modules (or equivalent).
 */
export async function walkToAlphaNotePage(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, [...ALPHA_BEFORE_NOTE_PAGE, ALPHA_NOTE_PAGE_STEP]);
  await expect(page.getByRole('heading', { name: 'Add to your learning log' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

export async function fillNoteBody(page: Page, body: string): Promise<void> {
  const field = page.locator('#note-body-field');
  await expect(field).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await field.fill(body);
}

export async function saveNoteAndContinue(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Save and continue');
  await expectPath(page, '/modules/alpha/content-pages/1-1-4');
}

/**
 * Save a note on the current content page and land on the next training page.
 */
export async function saveNoteOnCurrentPage(
  page: Page,
  body: string,
): Promise<void> {
  await fillNoteBody(page, body);
  await saveNoteAndContinue(page);
}

/**
 * Revisit the alpha note page after module start (soft-gated URLs) and save.
 */
export async function revisitAlphaNotePage(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}${ALPHA_NOTE_PAGE_PATH}`);
  await expectPath(page, ALPHA_NOTE_PAGE_PATH);
  await expect(page.getByRole('heading', { name: 'Add to your learning log' })).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}

/** Minimal module start so alpha appears on the learning log tabs. */
export async function startAlphaForLearningLog(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await continueFromInterruption(page);
}

export async function expectLearningLogNavLink(
  page: Page,
  visible: boolean,
): Promise<void> {
  const link = page.locator('nav').getByRole('link', { name: 'Learning log' });
  if (visible) {
    await expect(link).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  } else {
    await expect(link).toHaveCount(0);
  }
}

/**
 * Mirrors spec/system/learning_log_spec.rb navigation menu visibility.
 */
export async function expectLearningLogNavBeforeAndAfterModuleStart(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await page.goto(`${baseUrl}/`);
  await expectLearningLogNavLink(page, false);

  await page.goto(`${baseUrl}/my-modules`);
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);

  await page.goto(`${baseUrl}/`);
  await expectLearningLogNavLink(page, true);
}

export async function registerAndAssertLearningLogNavVisibility(
  page: Page,
  baseUrl: string,
  label: string,
): Promise<void> {
  await registerMinimalUser(page, baseUrl, { label });
  await expectLearningLogNavBeforeAndAfterModuleStart(page, baseUrl);
}
