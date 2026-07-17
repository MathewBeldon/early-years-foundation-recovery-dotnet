import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  openModuleFromCard,
  startModule,
} from './learning.js';
import {
  ALPHA_PASS_SKIP_FEEDBACK,
  clickTrainingAction,
  type ModuleStep,
  walkModuleSteps,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const FIRST_CONFIDENCE_QUESTION_PATH =
  '/modules/alpha/questionnaires/1-3-3-1';

const CONFIDENCE_INTRO_STEP = ALPHA_PASS_SKIP_FEEDBACK.find(
  (step) => step.path === '/modules/alpha/content-pages/1-3-3',
);
const CONFIDENCE_INTRO_INDEX = ALPHA_PASS_SKIP_FEEDBACK.findIndex(
  (step) => step.path === '/modules/alpha/content-pages/1-3-3',
);

if (CONFIDENCE_INTRO_INDEX < 0 || !CONFIDENCE_INTRO_STEP) {
  throw new Error('ALPHA_PASS_SKIP_FEEDBACK missing confidence intro step');
}

export const FIRST_CONFIDENCE_QUESTION_STEP: ModuleStep = {
  path: FIRST_CONFIDENCE_QUESTION_PATH,
  text: 'Question One - Select from 1 to 5',
  actions: [],
};

/** Pass walk through confidence intro; lands on first confidence question. */
export const ALPHA_TO_FIRST_CONFIDENCE_QUESTION: ModuleStep[] = [
  ...ALPHA_PASS_SKIP_FEEDBACK.slice(0, CONFIDENCE_INTRO_INDEX + 1),
  FIRST_CONFIDENCE_QUESTION_STEP,
];

export async function walkToFirstConfidenceQuestion(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, ALPHA_TO_FIRST_CONFIDENCE_QUESTION);
}

export async function expectFirstConfidenceQuestion(page: Page): Promise<void> {
  await expectPath(page, FIRST_CONFIDENCE_QUESTION_PATH);
  await expect(
    page.getByText('Question One - Select from 1 to 5').first(),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function expectConfidenceUsesRadios(page: Page): Promise<void> {
  await expect(page.locator('.govuk-radios__input').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(page.locator('.govuk-checkboxes__input')).toHaveCount(0);
}

export async function confidenceChoiceLabels(
  page: Page,
): Promise<{ first: string; second: string }> {
  const veryConfident = page.getByLabel('Very confident');
  if (await veryConfident.isVisible().catch(() => false)) {
    return { first: 'Very confident', second: 'Not very confident' };
  }
  return { first: 'Strongly agree', second: 'Disagree' };
}

export async function chooseConfidenceAnswer(
  page: Page,
  label: string,
): Promise<void> {
  await page.getByLabel(label, { exact: true }).check();
}

export async function advanceConfidenceQuestion(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Next');
}

export async function resumeModuleFromOverview(page: Page): Promise<void> {
  const resume = page.getByRole('link', { name: 'Resume module' }).first();
  await expect(resume).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await Promise.all([
    page.waitForURL(new RegExp(`${FIRST_CONFIDENCE_QUESTION_PATH}(?:\\?.*)?$`), {
      timeout: STEP_TIMEOUT_MS,
    }),
    resume.click(),
  ]);
  await expectPath(page, FIRST_CONFIDENCE_QUESTION_PATH);
}

export async function visitAlphaOverview(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/modules/alpha`, { waitUntil: 'domcontentloaded' });
  await expectPath(page, '/modules/alpha');
}
