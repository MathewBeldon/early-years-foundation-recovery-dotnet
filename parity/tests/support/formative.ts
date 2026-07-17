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
import { visitAlphaOverview } from './module-overview.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const FIRST_FORMATIVE_PATH = '/modules/alpha/questionnaires/1-1-4-1';
/** Correct answer index 1 — mirrors alpha-pass-response.yml and formative_question_spec. */
export const FIRST_FORMATIVE_CORRECT_ANSWER_FIELD = 'response-answers-1-field';
/** Wrong answer index 2 — mirrors alpha-fail-response.yml and response_spec answers: [2]. */
export const FIRST_FORMATIVE_WRONG_ANSWER_FIELD = 'response-answers-2-field';

/** Steps from module start up to (not including) the first formative question. */
export const ALPHA_BEFORE_FIRST_FORMATIVE: ModuleStep[] =
  ALPHA_PASS_SKIP_FEEDBACK.slice(0, 7);

export const FIRST_FORMATIVE_STEP: ModuleStep = {
  path: FIRST_FORMATIVE_PATH,
  text: 'Question One - Select from following',
  actions: [],
};

/**
 * Register path is caller-owned; starts from my-modules and lands on 1-1-4-1.
 */
export async function walkToFirstFormative(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, [
    ...ALPHA_BEFORE_FIRST_FORMATIVE,
    FIRST_FORMATIVE_STEP,
  ]);
}

export async function expectFirstFormativePage(page: Page): Promise<void> {
  await expectPath(page, FIRST_FORMATIVE_PATH);
  await expect(
    page.getByText('Question One - Select from following').first(),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function chooseFormativeAnswer(
  page: Page,
  fieldId: string,
): Promise<void> {
  const input = page.locator(`#${fieldId}`);
  await expect(input).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await input.check();
}

export async function submitFormativeAnswer(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Next');
}

export async function expectFormativeAnswerRequiredError(
  page: Page,
): Promise<void> {
  await expect(
    page.getByRole('link', { name: 'Please select an answer.' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(page.locator('#formative-results')).toHaveCount(0);
}

export async function expectFormativeInputsLocked(page: Page): Promise<void> {
  await expect(page.locator('.govuk-radios__input:disabled')).not.toHaveCount(0);
}

export async function expectCorrectFormativeResults(page: Page): Promise<void> {
  const results = page.locator('#formative-results');
  await expect(results).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(results.getByText("That's right")).toBeVisible();
  await expect(results.locator('.govuk-notification-banner--success')).toBeVisible();
  await expect(results.getByText("That's not quite right")).toHaveCount(0);
  await expectFormativeInputsLocked(page);
}

export async function expectWrongFormativeResults(page: Page): Promise<void> {
  const results = page.locator('#formative-results');
  await expect(results).toBeVisible({ timeout: STEP_TIMEOUT_MS });
  await expect(results.getByText("That's not quite right")).toBeVisible();
  await expect(results.locator('.govuk-notification-banner--success')).toHaveCount(
    0,
  );
  await expect(
    results.getByRole('heading', { name: 'Correct answer' }),
  ).toBeVisible();
  await expect(results.getByText('Correct answer 1', { exact: true })).toBeVisible();
  await expectFormativeInputsLocked(page);
}

export async function submitCorrectFormativeAnswer(page: Page): Promise<void> {
  await chooseFormativeAnswer(page, FIRST_FORMATIVE_CORRECT_ANSWER_FIELD);
  await submitFormativeAnswer(page);
}

export async function resumeFormativeFromOverview(
  page: Page,
  baseUrl: string,
): Promise<void> {
  await visitAlphaOverview(page, baseUrl);
  await page.getByRole('link', { name: 'Resume module' }).first().click();
}

export async function advanceFormativeAfterResults(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Next');
  await expectPath(page, '/modules/alpha/content-pages/1-2');
  await expect(page.getByText('The second submodule').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
}
