import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';
import {
  openModuleFromCard,
  startModule,
} from './learning.js';
import {
  ALPHA_FAIL,
  ALPHA_PASS_SKIP_FEEDBACK,
  clickTrainingAction,
  type ModuleStep,
  walkModuleSteps,
} from './module-journey.js';
import { expectPath } from './registration.js';

const STEP_TIMEOUT_MS = 30_000;

export const SUMMATIVE_INTRO_PATH = '/modules/alpha/content-pages/1-3-2';
export const FIRST_SUMMATIVE_QUESTION_PATH =
  '/modules/alpha/questionnaires/1-3-2-1';
export const ASSESSMENT_RESULT_PATH =
  '/modules/alpha/assessment-result/1-3-2-11';

/** Steps from module start through recap (before End of module test intro). */
export const ALPHA_BEFORE_SUMMATIVE_INTRO: ModuleStep[] =
  ALPHA_PASS_SKIP_FEEDBACK.slice(0, 15);

export const SUMMATIVE_INTRO_STEP: ModuleStep = {
  path: SUMMATIVE_INTRO_PATH,
  text: 'End of module test',
  actions: [],
};

const SUMMATIVE_INTRO_WITH_START = ALPHA_PASS_SKIP_FEEDBACK[15]!;

export const FIRST_SUMMATIVE_QUESTION_STEP: ModuleStep = {
  path: FIRST_SUMMATIVE_QUESTION_PATH,
  text: 'Question One - Select from following',
  actions: [],
};

/** Pass path through assessment results without advancing to confidence. */
export const ALPHA_THROUGH_PASSED_ASSESSMENT: ModuleStep[] = [
  ...ALPHA_PASS_SKIP_FEEDBACK.slice(0, 26),
  {
    path: ASSESSMENT_RESULT_PATH,
    text: 'Assessment results',
    actions: [],
  },
];

export const WRONG_ANSWER_QUESTION_LABELS = [
  'Question One',
  'Question Two',
  'Question Three',
  'Question Four',
  'Question Five',
  'Question Six',
  'Question Seven',
  'Question Eight',
  'Question Nine',
  'Question Ten',
] as const;

export async function walkToSummativeIntro(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, [
    ...ALPHA_BEFORE_SUMMATIVE_INTRO,
    SUMMATIVE_INTRO_STEP,
  ]);
}

export async function walkToFirstSummativeQuestion(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, [
    ...ALPHA_BEFORE_SUMMATIVE_INTRO,
    SUMMATIVE_INTRO_WITH_START,
    FIRST_SUMMATIVE_QUESTION_STEP,
  ]);
}

export async function walkToPassedAssessment(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, ALPHA_THROUGH_PASSED_ASSESSMENT);
}

export async function walkToFailedAssessment(page: Page): Promise<void> {
  await openModuleFromCard(page, /First Training Module/);
  await startModule(page);
  await walkModuleSteps(page, ALPHA_FAIL);
}

export async function expectSummativeIntro(page: Page): Promise<void> {
  await expectPath(page, SUMMATIVE_INTRO_PATH);
  await expect(page.getByText('End of module test').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(
    page.getByText(
      'This end of module test is here to revisit what you have learned',
    ),
  ).toBeVisible();
}

export async function expectSummativePassmark(page: Page): Promise<void> {
  await expect(
    page.getByText(
      'If you do not score 70%, you will be able to see which questions you got wrong.',
    ),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function submitSummativeWithoutAnswer(page: Page): Promise<void> {
  await clickTrainingAction(page, 'Save and continue');
}

export async function expectSummativeAnswerRequiredError(
  page: Page,
): Promise<void> {
  await expect(
    page.getByRole('link', { name: 'Please select an answer.' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function expectPerfectSummativeResults(page: Page): Promise<void> {
  await expectPath(page, ASSESSMENT_RESULT_PATH);
  const results = page.locator('#assessment-results');
  await expect(results.getByText('You scored 100%')).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(
    results.getByRole('heading', { name: 'Congratulations' }),
  ).toBeVisible();
  await expect(
    results.getByText(
      /you have scored highly enough to receive a certificate of achievement for this module/i,
    ),
  ).toBeVisible();
  for (const label of WRONG_ANSWER_QUESTION_LABELS) {
    await expect(results.getByText(label, { exact: true })).toHaveCount(0);
  }
}

export async function expectFailedSummativeResultsWithWrongQuestions(
  page: Page,
): Promise<void> {
  await expectPath(page, ASSESSMENT_RESULT_PATH);
  const results = page.locator('#assessment-results');
  await expect(results.getByText(/You scored \d+%/)).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(results.getByText('Revisit the module')).toBeVisible();
  await expect(
    results.getByText(
      /Unfortunately you have not scored highly enough to receive a certificate/,
    ),
  ).toBeVisible();
  // Wrong answers listed — labels may include suffixes beyond "Question One".
  await expect(results.getByText(/Question One/)).toBeVisible();
  await expect(results.getByText(/Question Ten/)).toBeVisible();
}

export async function expectSummativeInputsDisabled(page: Page): Promise<void> {
  await expect(page.locator('.govuk-checkboxes__input:disabled')).not.toHaveCount(
    0,
  );
}

export async function expectSummativeInputsEnabled(page: Page): Promise<void> {
  await expect(page.locator('.govuk-checkboxes__input:disabled')).toHaveCount(0);
}

export async function expectReflectLinkOnOverview(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha');
  await expect(
    page.getByRole('link', { name: 'Reflect on your learning' }),
  ).toBeVisible({ timeout: STEP_TIMEOUT_MS });
}

export async function expectReflectTextWithoutLink(page: Page): Promise<void> {
  await expectPath(page, '/modules/alpha');
  await expect(page.getByText('Reflect on your learning').first()).toBeVisible({
    timeout: STEP_TIMEOUT_MS,
  });
  await expect(
    page.getByRole('link', { name: 'Reflect on your learning' }),
  ).toHaveCount(0);
}

export async function visitAlphaOverview(page: Page, baseUrl: string): Promise<void> {
  await page.goto(`${baseUrl}/modules/alpha`, { waitUntil: 'domcontentloaded' });
  await expectPath(page, '/modules/alpha');
}
